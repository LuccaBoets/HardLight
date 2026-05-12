using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.NullSpace;

/// <summary>
/// Marks an entity as a bluespace flasher and stores its trigger radius for client-side NullSpace rendering.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BluespaceFlasherVisualsComponent : Component
{
    [DataField]
    public float Radius = 10f;
}
