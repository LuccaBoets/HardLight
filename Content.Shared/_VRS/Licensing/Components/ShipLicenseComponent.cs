using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._VRS.Licensing;

/// <summary>
/// Marks an item as licensed to a specific ship (identified by the ship's deed <see cref="OwnerShuttleUid"/>).
/// Licensed items can only be anchored on that ship. Bootleg licenses (produced by emag/hacking) bypass
/// the ship check but mark the item as contraband.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipLicenseComponent : Component
{
    /// <summary>
    /// NetEntity of the owning shuttle/grid. Comes from <c>ShuttleDeedComponent.ShuttleUid</c>.
    /// Null when <see cref="Bootleg"/> is true (a hacked license has no real owner).
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity? OwnerShuttleUid;

    /// <summary>
    /// Display name of the licensed ship at the time of stamping (purely cosmetic for examine text).
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? OwnerShuttleName;

    /// <summary>
    /// True if this license was created by emag/hack rather than a legitimate licensing console.
    /// Bootleg licenses allow anchoring on any grid but mark the item as contraband and degrade resale.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Bootleg;
}
