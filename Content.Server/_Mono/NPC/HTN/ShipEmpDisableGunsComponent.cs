using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Marks a ship AI core whose weapon systems and thrusters should be disabled by EMP.
/// <see cref="DurationMultiplier"/> scales the incoming EMP duration — lower values mean
/// the ship recovers faster, modelling larger/more hardened vessels having better shielding.
/// </summary>
[RegisterComponent]
public sealed partial class ShipEmpDisableGunsComponent : Component
{
    /// <summary>
    /// Multiplier applied to the incoming EMP disable duration before it is applied.
    /// 1.0 = full duration (small/unshielded). 0.5 = half duration (medium). 0.25 = quarter (large/hub).
    /// </summary>
    [DataField]
    public float DurationMultiplier = 1.0f;
}
