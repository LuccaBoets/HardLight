namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Tracks "aggro" state for an AI ship core. While aggroed, the HTN
/// blackboard key <see cref="BlackboardKey"/> is present so HTN compounds
/// can branch into a chase/attack behavior. Aggro is triggered by either
/// taking incoming ship-weapon fire on the parent grid, or by a hostile
/// shuttle target entering <see cref="AggroProximityRange"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ShipAggroComponent : Component
{
    /// <summary>
    /// Distance at which a hostile ShipNpcTarget triggers aggro by proximity.
    /// </summary>
    [DataField]
    public float AggroProximityRange = 500f;

    /// <summary>
    /// While aggroed, distance within which a hostile ShipNpcTarget keeps
    /// aggro refreshed. Aggro only fades once the target is past this
    /// range AND <see cref="AggroDuration"/> has elapsed since the last
    /// trigger. Should be larger than <see cref="AggroProximityRange"/>
    /// so the AI doesn't immediately re-engage after dropping aggro.
    /// </summary>
    [DataField]
    public float AggroLeashRange = 1000f;

    /// <summary>
    /// How long aggro persists after the most recent trigger (damage or
    /// proximity / in-leash). Refreshed every tick the trigger is still
    /// active.
    /// </summary>
    [DataField]
    public TimeSpan AggroDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Server time at which aggro currently expires. Default = never aggroed.
    /// </summary>
    [DataField]
    public TimeSpan AggroEndTime = TimeSpan.Zero;

    /// <summary>
    /// Blackboard key set on the HTN while aggroed.
    /// </summary>
    [DataField]
    public string BlackboardKey = "Aggroed";

    /// <summary>
    /// Distance from any player station grid below which this AI core
    /// should disengage and steer away. While inside this range, aggro
    /// is force-cleared (so the AI loses interest in the player) and the
    /// HTN blackboard key <see cref="AvoidStationBlackboardKey"/> is set
    /// to an <see cref="Robust.Shared.Map.EntityCoordinates"/> waypoint
    /// just outside the avoidance border, pointing directly away from the
    /// nearest station grid. Set to 0 to disable.
    /// </summary>
    [DataField]
    public float AvoidStationRange = 0f;

    /// <summary>
    /// Extra distance added past <see cref="AvoidStationRange"/> when
    /// computing the flee waypoint, so the AI commits to a target a bit
    /// outside the no-go border instead of skimming it.
    /// </summary>
    [DataField]
    public float AvoidStationBuffer = 500f;

    /// <summary>
    /// Blackboard key the flee waypoint is written to while inside the
    /// station avoidance range.
    /// </summary>
    [DataField]
    public string AvoidStationBlackboardKey = "AvoidStationCoordinates";

    /// <summary>
    /// Latest computed flee waypoint, mirrored onto the blackboard every
    /// tick. <c>null</c> when the AI is outside the avoidance range.
    /// Not serialized: this is recomputed each scan from current
    /// positions.
    /// </summary>
    [ViewVariables]
    public Robust.Shared.Map.EntityCoordinates? PendingAvoidStationCoordinates;
}
