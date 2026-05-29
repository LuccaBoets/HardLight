using Content.Server.Objectives.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.CCVar;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.Configuration;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles kill person condition logic and picking random kill targets.
/// </summary>
public sealed class KillPersonConditionSystem : EntitySystem
{
    [Dependency] private readonly RoundEndArrivalSystem _arrival = default!; // HardLight
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KillPersonConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, KillPersonConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (!_target.GetTarget(uid, out var target))
            return;

        args.Progress = GetProgress(target.Value, comp.RequireDead, comp.RequireMaroon);
    }

    private float GetProgress(EntityUid target, bool requireDead, bool requireMaroon)
    {
        // deleted or gibbed or something, counts as dead
        if (!TryComp<MindComponent>(target, out var mind) || mind.OwnedEntity == null)
            return 1f;

        var targetDead = _mind.IsCharacterDeadIc(mind);
        if (!_config.GetCVar(CCVars.EmergencyShuttleEnabled) && requireMaroon)
        {
            requireDead = true;
            requireMaroon = false;
        }

        if (requireDead && !targetDead)
            return 0f;

        // HardLight start
        // Maroon-style objectives now resolve on the target's round-end survival state
        // rather than whether they physically boarded the evac shuttle.
        // HardLight end
        if (requireMaroon && !_emergencyShuttle.ShuttlesLeft)
            return targetDead ? 0.5f : 0f; // HardLight: targetOnShuttle<targetDead

        // HardLight start
        // Once the round has actually ended, only targets that actually ended up
        // at a safe arrival destination count as having made it.
        // HardLight end
        if (requireMaroon && _emergencyShuttle.ShuttlesLeft)
            return targetDead || !_arrival.CountsAsArrived(mind.OwnedEntity.Value) ? 1f : 0f; // HardLight

        // HardLight: If evac is disabled, requireMaroon is normalized away above.
        if (requireMaroon)
            return 0f;

        return 1f; // Good job you did it woohoo
    }
}
