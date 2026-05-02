using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RadarTileMaskComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<string> HiddenTileIds = new();
}