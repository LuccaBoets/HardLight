using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Server._Common.Consent;
using Content.Shared._Common.Consent;
using Content.Shared._HL.PlayerMoods;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._HL.PlayerMoods;

/// <summary>
/// Manages composable erotic mood pool traits for any species.
///
/// Architecture:
///   - Each <see cref="EroticMoodPoolComponentBase"/> subtype is an independently
///     selectable trait; players may combine any number of pools.
///   - Moods are rolled from <see cref="PlayerMoodPrototype"/> entries tagged
///     <c>type: mood</c> in YAML, not from thavenMood prototypes.
///   - Per-component <see cref="TimeSpan"/> timestamps drive reminders and re-rolls.
///     The Update loop is gated to 1 check/min.
///   - Before a mood is shown, its <see cref="PlayerMoodPrototype.RequiredConsent"/>
///     (if set) is checked against the player's consent settings; ineligible moods
///     are skipped and replaced during the same roll.
/// </summary>
public sealed class PlayerMoodSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly ConsentSystem _consent = default!;

    private static readonly Color MoodColor = Color.FromHex("#AAAAAA");
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VanillaEroticMoodsComponent, ComponentStartup>((uid, comp, _) => OnPoolStartup(uid, comp));
        SubscribeLocalEvent<KinkyEroticMoodsComponent, ComponentStartup>((uid, comp, _) => OnPoolStartup(uid, comp));
        SubscribeLocalEvent<RomanticMoodsComponent, ComponentStartup>((uid, comp, _) => OnPoolStartup(uid, comp));
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _nextCheck = TimeSpan.Zero);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime < _nextCheck)
            return;

        _nextCheck = curTime + CheckInterval;

        HandleMoodPools<VanillaEroticMoodsComponent>(curTime);
        HandleMoodPools<KinkyEroticMoodsComponent>(curTime);
        HandleMoodPools<RomanticMoodsComponent>(curTime);
    }

    // --- Initialization ---

    private void OnPoolStartup(EntityUid uid, EroticMoodPoolComponentBase comp)
    {
        var curTime = _timing.CurTime;
        RollMoods(uid, comp);
        comp.NextReminder = curTime + comp.ReminderBase + Jitter(comp.ReminderJitter);
        comp.NextReroll = curTime + comp.RerollBase + Jitter(comp.RerollJitter);
        TrySendMoodMessage(uid, comp, isNewRoll: true);
    }

    // --- Periodic update (runs at most 1×/min) ---

    private void HandleMoodPools<T>(TimeSpan curTime) where T : EroticMoodPoolComponentBase
    {
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (curTime >= comp.NextReroll)
            {
                RollMoods(uid, comp);
                comp.NextReminder = curTime + comp.ReminderBase + Jitter(comp.ReminderJitter);
                comp.NextReroll = curTime + comp.RerollBase + Jitter(comp.RerollJitter);
                TrySendMoodMessage(uid, comp, isNewRoll: true);
            }
            else if (curTime >= comp.NextReminder)
            {
                comp.NextReminder = curTime + comp.ReminderBase + Jitter(comp.ReminderJitter);
                TrySendMoodMessage(uid, comp, isNewRoll: false);
            }
        }
    }

    // --- Mood rolling ---

    private void RollMoods(EntityUid uid, EroticMoodPoolComponentBase comp)
    {
        if (!_proto.TryIndex<DatasetPrototype>(comp.DatasetId, out var dataset))
        {
            Log.Warning($"[PlayerMoodSystem] Dataset '{comp.DatasetId}' not found.");
            return;
        }

        comp.ActiveMoods.Clear();

        var choices = dataset.Values.ToList();
        var conflicts = new HashSet<string>();
        var department = GetDepartment(uid);

        while (comp.ActiveMoods.Count < comp.MoodsPerCycle && choices.Count > 0)
        {
            var id = _random.PickAndTake(choices);

            if (conflicts.Contains(id))
                continue;
            if (!_proto.TryIndex<PlayerMoodPrototype>(id, out var proto))
                continue;
            if (department != null && proto.Conflicts.Contains(department))
                continue;

            // Consent gate: skip if the player hasn't enabled the required consent.
            if (proto.RequiredConsent != null && !_consent.HasConsent(uid, proto.RequiredConsent))
                continue;

            var mood = new PlayerMood
            {
                ProtoId = proto.ID,
                MoodName = proto.MoodName,
                MoodDesc = proto.MoodDesc,
                Conflicts = proto.Conflicts,
                RequiredConsent = proto.RequiredConsent,
                MoodVars = RollMoodVars(proto),
            };

            comp.ActiveMoods.Add(mood);
            conflicts.Add(proto.ID);
            conflicts.UnionWith(proto.Conflicts);
        }
    }

    private Dictionary<string, string> RollMoodVars(PlayerMoodPrototype proto)
    {
        if (proto.MoodVarDatasets.Count == 0)
            return new Dictionary<string, string>();

        var vars = new Dictionary<string, string>();
        var alreadyChosen = new HashSet<string>();

        foreach (var (varName, datasetId) in proto.MoodVarDatasets)
        {
            if (!_proto.TryIndex<DatasetPrototype>(datasetId, out var dataset))
                continue;

            if (proto.AllowDuplicateMoodVars)
            {
                vars[varName] = _random.Pick(dataset.Values);
                continue;
            }

            var choices = dataset.Values.ToList();
            var found = false;
            while (choices.Count > 0)
            {
                var choice = _random.PickAndTake(choices);
                if (alreadyChosen.Contains(choice))
                    continue;
                vars[varName] = choice;
                alreadyChosen.Add(choice);
                found = true;
                break;
            }

            if (!found && dataset.Values.Count > 0)
                vars[varName] = _random.Pick(dataset.Values);
        }

        return vars;
    }

    // --- Chat delivery ---

    private void TrySendMoodMessage(EntityUid uid, EroticMoodPoolComponentBase comp, bool isNewRoll)
    {
        if (!TryComp<ActorComponent>(uid, out var actor) || comp.ActiveMoods.Count == 0)
            return;

        var headerKey = isNewRoll ? "erotic-moods-header-new-roll" : "erotic-moods-header-reminder";
        var sb = new StringBuilder(Loc.GetString(headerKey));

        foreach (var mood in comp.ActiveMoods)
        {
            sb.Append("\n");
            if (isNewRoll)
            {
                sb.Append(mood.GetLocName());
                sb.Append(": ");
            }
            sb.Append(mood.GetLocDesc());
        }

        var msg = sb.ToString();
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
        _chatManager.ChatMessageToOne(
            ChatChannel.Server,
            msg,
            wrapped,
            default,
            false,
            actor.PlayerSession.Channel,
            colorOverride: MoodColor);
    }

    // --- Helpers ---

    /// <summary>Returns a random offset in [-range/2, +range/2].</summary>
    private TimeSpan Jitter(TimeSpan range) =>
        TimeSpan.FromSeconds((_random.NextDouble() - 0.5) * range.TotalSeconds);

    private string? GetDepartment(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return null;
        if (!_jobs.MindTryGetJobId(mindId, out var jobName) || jobName == null)
            return null;
        if (!_jobs.TryGetDepartment(jobName, out var dept))
            return null;
        return dept.ID;
    }
}
