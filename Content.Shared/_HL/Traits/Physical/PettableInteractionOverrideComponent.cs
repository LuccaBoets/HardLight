using Robust.Shared.GameStates;

namespace Content.Shared._HL.Traits.Physical;

/// <summary>
/// Marks a humanoid so their default hug popup is rewritten to the kitsune-style pet interaction on both client and server.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PettableInteractionOverrideComponent : Component
{
}
