using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.NullSpace;

/// <summary>
/// Component that actively scans for NullSpace entities and pulses to remove them.
/// Replaces proximity-trigger detection which NullSpace entities can't participate in
/// (their physics contacts are all cancelled).
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BluespacePulseOnTriggerComponent : Component
{
    [DataField]
    public float Radius = 10f;

    [DataField]
    public float StunSeconds = 4f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTrigger;
}
