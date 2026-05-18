// VRS port: Goobstation/Starlight CollectiveMind — entity component that
// holds a per-entity set of joined collective mind channels. Each joined
// channel is also assigned an ascending per-channel "number" so chat
// receivers can address speakers by channel number even when names are
// hidden.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.CollectiveMind;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CollectiveMindComponent : Component
{
    [DataField]
    public Dictionary<string, int> Minds = new();

    [DataField, AutoNetworkedField]
    public ProtoId<CollectiveMindPrototype>? DefaultChannel = null;

    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<CollectiveMindPrototype>> Channels = new();

    [DataField]
    public bool HearAll = false;

    [DataField]
    public bool SeeAllNames = false;
}
