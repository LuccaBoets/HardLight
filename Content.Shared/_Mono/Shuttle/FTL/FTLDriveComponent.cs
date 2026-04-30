using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Mono.Shuttle.FTL;

[RegisterComponent]
public sealed partial class FTLDriveComponent : Component
{
    /// <summary>
    /// Effective range in meters (or prototype units).
    /// </summary>
    [DataField("range")]
    public int Range { get; set; } = 0;

    /// <summary>
    /// Thermal signature emitted when operating, used by sensors/radar.
    /// </summary>
    [DataField("thermalSignature")]
    public int ThermalSignature { get; set; } = 0;

    /// <summary>
    /// Multiplier applied to all FTL timings (startup, travel, cooldown).
    /// Values below 1.0 speed up FTL; 0 means no effect (drive does not modify timings).
    /// When multiple drives are present the worst (highest) factor wins to prevent stacking.
    /// </summary>
    [DataField("speedFactor")]
    public float SpeedFactor { get; set; } = 0f;
}
