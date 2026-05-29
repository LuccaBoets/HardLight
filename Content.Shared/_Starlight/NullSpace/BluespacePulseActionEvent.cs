using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.NullSpace;

/// <summary>
/// Server-side action event that purges NullSpaceComponent entities within a radius and stuns them.
/// Not network-serialized.
/// </summary>
public sealed partial class BluespacePulseActionEvent : InstantActionEvent
{
    [DataField]
    public EntityUid Source;

    [DataField]
    public float Radius = 10f;

    [DataField]
    public float StunSeconds = 4f;
}
