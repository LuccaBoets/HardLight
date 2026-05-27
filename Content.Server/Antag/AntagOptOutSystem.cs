using System.Linq;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Antag;

/// <summary>
/// VRS: tracks per-life antag opt-outs and cross-round "rounds since last antag" counters.
/// Used by <see cref="AntagSelectionSystem"/> to filter the selection pool and weight selection
/// against players who were antag recently.
/// </summary>
/// <remarks>
/// Opt-outs persist until any of: the player disconnects, the player respawns
/// (<see cref="PlayerSpawnCompleteEvent"/> — covers character changes, cryo-out-then-back-in,
/// taking a ghost role), the round restarts, or the server restarts. Rounds-since counters
/// persist across rounds but reset on server restart. The user has explicitly accepted these
/// trade-offs (DB persistence is overkill for MRP-scale rotation tuning).
/// </remarks>
public sealed class AntagOptOutSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>NetUserIds who have opted out of antag selection for their current life.</summary>
    private readonly HashSet<NetUserId> _optedOut = new();

    /// <summary>
    /// Rounds since each player was last selected as an antagonist. Players not in the dict are
    /// treated as "never antag" (max weight).
    /// </summary>
    private readonly Dictionary<NetUserId, int> _roundsSinceAntag = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<AntagSelectionComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        // Increment rounds-since for every tracked player.
        var keys = _roundsSinceAntag.Keys.ToArray();
        foreach (var key in keys)
            _roundsSinceAntag[key]++;

        // Everyone respawns at round start; opt-outs are per-life so they all clear.
        _optedOut.Clear();
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        // A respawn (character change, cryo-then-rejoin, ghost-role takeover, etc.) ends the
        // current "life" — drop any opt-out so the new character starts with a clean slate.
        _optedOut.Remove(ev.Player.UserId);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus is SessionStatus.Disconnected or SessionStatus.Zombie)
            _optedOut.Remove(args.Session.UserId);
    }

    private void OnAntagSelected(Entity<AntagSelectionComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session is not { } session)
            return;

        // Reset the counter — this player was antag this round.
        _roundsSinceAntag[session.UserId] = 0;
    }

    /// <summary>
    /// Marks the given session as opted out of antag selection for the rest of their current
    /// life (cleared on respawn, disconnect, round restart, or server restart). One-way: there
    /// is no opt-back-in API by design.
    /// </summary>
    public void OptOut(NetUserId userId) => _optedOut.Add(userId);

    /// <summary>Convenience overload for callers that have an <see cref="ICommonSession"/>.</summary>
    public void OptOut(ICommonSession session) => OptOut(session.UserId);

    /// <summary>Whether the given session is currently opted out of antag selection.</summary>
    public bool IsOptedOut(NetUserId userId) => _optedOut.Contains(userId);

    /// <summary>Convenience overload for callers that have an <see cref="ICommonSession"/>.</summary>
    public bool IsOptedOut(ICommonSession session) => IsOptedOut(session.UserId);

    /// <summary>
    /// Returns the selection weight for a player based on how many rounds since they last
    /// antagged. Range [1.0, <see cref="CCVars.AntagRepeatWeightMax"/>]. Players never seen
    /// before (no entry in the counter dict) get the max weight.
    /// </summary>
    public float GetSelectionWeight(NetUserId userId)
    {
        var perRound = _cfg.GetCVar(CCVars.AntagRepeatWeightPerRound);
        var max = _cfg.GetCVar(CCVars.AntagRepeatWeightMax);

        if (perRound <= 0f || max <= 1f)
            return 1f;

        // Untracked players (never antagged, or freshly connected) get max weight — they should
        // be prioritised over players who have a history of antagging.
        if (!_roundsSinceAntag.TryGetValue(userId, out var rounds))
            return max;

        var weight = 1f + perRound * rounds;
        return MathF.Min(max, MathF.Max(1f, weight));
    }

    /// <summary>
    /// Picks one session from the given list using anti-repeat weights, removes it from the list,
    /// and returns it. Returns false if the list is empty.
    /// </summary>
    /// <remarks>
    /// Use this in place of <see cref="IRobustRandom.PickAndTake{T}(IList{T})"/> inside antag
    /// pool draws so recent antags are biased against.
    /// </remarks>
    public bool TryWeightedPickAndTake(IList<ICommonSession> list, out ICommonSession? picked)
    {
        picked = null;
        if (list.Count == 0)
            return false;

        // Compute weights and total.
        var weights = new float[list.Count];
        var total = 0f;
        for (var i = 0; i < list.Count; i++)
        {
            weights[i] = GetSelectionWeight(list[i].UserId);
            total += weights[i];
        }

        if (total <= 0f)
        {
            // Degenerate case (shouldn't happen since min weight is 1.0) — fall back to uniform.
            var idx = _random.Next(list.Count);
            picked = list[idx];
            list.RemoveAt(idx);
            return true;
        }

        var roll = _random.NextFloat() * total;
        var acc = 0f;
        for (var i = 0; i < list.Count; i++)
        {
            acc += weights[i];
            if (roll <= acc)
            {
                picked = list[i];
                list.RemoveAt(i);
                return true;
            }
        }

        // Floating point edge: last element.
        picked = list[^1];
        list.RemoveAt(list.Count - 1);
        return true;
    }
}
