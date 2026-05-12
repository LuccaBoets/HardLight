using Robust.Shared.GameStates;

namespace Content.Shared.Shadowkin;

/// <summary>
/// Component for entities that are in an ethereal state.
/// Allows them to phase through solid objects and makes them less visible.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EtherealComponent : Component
{
    /// <summary>
    /// Whether the entity should appear darkened while ethereal.
    /// </summary>
    [DataField]
    public bool Darken = false;
}
