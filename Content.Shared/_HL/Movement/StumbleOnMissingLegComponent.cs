using Robust.Shared.GameStates;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Shared._HL.Movement;

/// <summary>
/// Causes the entity to periodically fall over while moving with one or more
/// missing legs (but at least one remaining). Combined with the existing
/// movement-speed reduction this produces a "limp until you fall" mechanic.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StumbleOnMissingLegComponent : Component
{
    /// <summary>
    /// Minimum distance (in tiles) the entity must walk on a missing leg
    /// before another stumble check can occur. Acts as a cooldown.
    /// </summary>
    [DataField]
    public float StepsBetweenChecks = 10f;

    /// <summary>
    /// Probability [0..1] of actually stumbling each time the distance
    /// threshold is reached. Default is intentionally low so stumbles are
    /// rare rather than guaranteed.
    /// </summary>
    [DataField]
    public float StumbleChance = 0.15f;

    /// <summary>
    /// How long the entity stays knocked down after stumbling.
    /// </summary>
    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// Maximum single-tick movement (tiles) that is still counted as walking.
    /// Larger jumps (teleports, FTL, parent changes) are ignored to avoid
    /// an instant stumble after relocation.
    /// </summary>
    [DataField]
    public float MaxStepDelta = 1.5f;

    /// <summary>
    /// Distance accumulated since the last stumble while legs are incomplete.
    /// </summary>
    [ViewVariables]
    public float DistanceTraveled;

    /// <summary>
    /// Last world position observed by the system.
    /// </summary>
    [ViewVariables]
    public Vector2? LastPosition;

    /// <summary>
    /// Last map id observed; if the entity changes maps the accumulator resets.
    /// </summary>
    [ViewVariables]
    public MapId? LastMap;
}
