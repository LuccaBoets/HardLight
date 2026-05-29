using Content.Server.Objectives.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.CCVar; // HardLight
using Content.Shared.Cuffs.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.Configuration; // HardLight

namespace Content.Server.Objectives.Systems;

public sealed class EscapeShuttleConditionSystem : EntitySystem
{
    [Dependency] private readonly RoundEndArrivalSystem _arrival = default!; // HardLight
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly IConfigurationManager _config = default!; // HardLight
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EscapeShuttleConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, EscapeShuttleConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(args.MindId, args.Mind);
    }

    private float GetProgress(EntityUid mindId, MindComponent mind)
    {
        // not escaping alive if you're deleted/dead
        if (mind.OwnedEntity == null || _mind.IsCharacterDeadIc(mind))
            return 0f;

        // You're not escaping if you're restrained!
        if (TryComp<CuffableComponent>(mind.OwnedEntity, out var cuffed) && cuffed.CuffedHandCount > 0)
            return 0f;

        // HardLight: If evac is disabled, surviving alive and uncuffed is enough.
        if (!_config.GetCVar(CCVars.EmergencyShuttleEnabled))
            return 1f;

        // HardLight: Reaching ColComm now resolves on the round-end destination rather than
        // whether the player physically boarded the evac shuttle.
        return _emergencyShuttle.ShuttlesLeft && _arrival.CountsAsArrived(mind.OwnedEntity.Value) ? 1f : 0f;
    }
}
