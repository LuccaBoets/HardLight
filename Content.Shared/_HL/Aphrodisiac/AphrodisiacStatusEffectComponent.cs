using Robust.Shared.GameStates;

namespace Content.Shared._HL.Aphrodisiac;

/// <summary>
/// This is used by a status effect entity to apply the <see cref="AphrodisiacComponent"/> to an entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AphrodisiacStatusEffectComponent : Component
{
}
