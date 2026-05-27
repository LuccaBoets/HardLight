using Robust.Shared.GameStates;

namespace Content.Shared._VRS.Licensing;

/// <summary>
/// One-shot marker indicating that the next successful anchoring of this entity should
/// automatically stamp a <see cref="ShipLicenseComponent"/> bound to the target grid's deed,
/// free of charge. Added by flatpack unpack and manual construction completion so legitimately
/// built machines license themselves to the ship they were built on. Removed on first anchor
/// (whether the auto-stamp happens or not).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShipLicensePendingComponent : Component
{
}
