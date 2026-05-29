using Content.Shared._HL.Movement;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.HL.CCVar;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._HL.Movement;

/// <summary>
/// Drives <see cref="StumbleOnMissingLegComponent"/>: while the body has a
/// partial leg count (at least one leg, but fewer than required) AND is
/// player-controlled, accumulates world-space movement and forces a brief
/// knockdown once the configured step threshold is exceeded.
/// </summary>
/// <remarks>
/// To keep per-tick cost negligible, the driver only iterates entities that
/// currently carry the <see cref="ActiveStumbleOnMissingLegComponent"/>
/// marker. The marker is added/removed reactively when the leg count, player
/// attachment, or component lifecycle changes.
/// </remarks>
public sealed class StumbleOnMissingLegSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    // Cached CVar values; refreshed via OnValueChanged so the hot loop never
    // hits the config manager. <=0 means "fall back to component default".
    private float _cvarChance;
    private float _cvarTiles;
    private float _cvarKnockdownSeconds;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(HLCCVars.StumbleChance, v => _cvarChance = v, true);
        _cfg.OnValueChanged(HLCCVars.StumbleTiles, v => _cvarTiles = v, true);
        _cfg.OnValueChanged(HLCCVars.StumbleKnockdownSeconds, v => _cvarKnockdownSeconds = v, true);

        SubscribeLocalEvent<StumbleOnMissingLegComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StumbleOnMissingLegComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StumbleOnMissingLegComponent, BodyPartAddedEvent>(OnBodyPartChanged);
        SubscribeLocalEvent<StumbleOnMissingLegComponent, BodyPartRemovedEvent>(OnBodyPartChanged);
        SubscribeLocalEvent<StumbleOnMissingLegComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<StumbleOnMissingLegComponent, PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnStartup(Entity<StumbleOnMissingLegComponent> ent, ref ComponentStartup args)
        => Refresh(ent);

    private void OnShutdown(Entity<StumbleOnMissingLegComponent> ent, ref ComponentShutdown args)
        => RemComp<ActiveStumbleOnMissingLegComponent>(ent);

    private void OnBodyPartChanged<T>(Entity<StumbleOnMissingLegComponent> ent, ref T args)
        => Refresh(ent);

    private void OnPlayerAttached(Entity<StumbleOnMissingLegComponent> ent, ref PlayerAttachedEvent args)
        => Refresh(ent);

    private void OnPlayerDetached(Entity<StumbleOnMissingLegComponent> ent, ref PlayerDetachedEvent args)
        => RemComp<ActiveStumbleOnMissingLegComponent>(ent);

    private void Refresh(Entity<StumbleOnMissingLegComponent> ent)
    {
        if (!HasComp<ActorComponent>(ent) || !TryComp(ent, out BodyComponent? body))
        {
            RemComp<ActiveStumbleOnMissingLegComponent>(ent);
            ResetState(ent.Comp);
            return;
        }

        var legs = body.LegEntities.Count;
        var partial = body.RequiredLegs > 0 && legs > 0 && legs < body.RequiredLegs;

        if (partial)
        {
            EnsureComp<ActiveStumbleOnMissingLegComponent>(ent);
        }
        else
        {
            RemComp<ActiveStumbleOnMissingLegComponent>(ent);
            ResetState(ent.Comp);
        }
    }

    private static void ResetState(StumbleOnMissingLegComponent comp)
    {
        comp.DistanceTraveled = 0f;
        comp.LastPosition = null;
        comp.LastMap = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Snapshot CVar overrides once per tick. Negative chance fully
        // disables; otherwise a non-positive override means "use the
        // per-component DataField default" so prototypes can still customize.
        if (_cvarChance < 0f)
            return;

        var chanceOverride = _cvarChance;
        var tilesOverride = _cvarTiles;
        var knockOverride = _cvarKnockdownSeconds;

        var query = EntityQueryEnumerator<ActiveStumbleOnMissingLegComponent, StumbleOnMissingLegComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var comp, out var xform))
        {
            // Only accrue distance while standing on a real map.
            if (xform.MapID == MapId.Nullspace || _standing.IsDown(uid))
            {
                comp.LastPosition = null;
                continue;
            }

            var pos = _xform.GetWorldPosition(xform);

            if (comp.LastMap != xform.MapID)
            {
                comp.LastMap = xform.MapID;
                comp.LastPosition = pos;
                continue;
            }

            if (comp.LastPosition is { } last)
            {
                var deltaSq = (pos - last).LengthSquared();
                var maxSq = comp.MaxStepDelta * comp.MaxStepDelta;
                if (deltaSq <= maxSq)
                    comp.DistanceTraveled += deltaSq; // squared-tiles, no sqrt
            }

            comp.LastPosition = pos;

            var tiles = tilesOverride > 0f ? tilesOverride : comp.StepsBetweenChecks;
            var thresholdSq = tiles * tiles;
            if (comp.DistanceTraveled < thresholdSq)
                continue;

            comp.DistanceTraveled = 0f;

            var chance = chanceOverride > 0f ? chanceOverride : comp.StumbleChance;
            if (chance <= 0f || !_random.Prob(chance))
                continue;

            var duration = knockOverride > 0f
                ? TimeSpan.FromSeconds(knockOverride)
                : comp.KnockdownDuration;

            if (_stun.TryKnockdown(uid, duration, refresh: true))
                _popup.PopupEntity(Loc.GetString("stumble-missing-leg-popup"), uid, uid, PopupType.MediumCaution);
        }
    }
}
