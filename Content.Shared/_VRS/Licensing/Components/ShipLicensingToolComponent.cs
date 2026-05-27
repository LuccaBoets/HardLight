// VRS: ship licensing tool — turns the appraisal tool into a dual-mode appraise/license device.
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._VRS.Licensing;

[Serializable, NetSerializable]
public enum ShipLicensingToolMode : byte
{
    Appraise = 0,
    License = 1,
}

/// <summary>
/// Augments an appraisal tool with a "License" mode. In Appraise mode the tool behaves as a normal
/// price gun (handled by <c>SharedPriceGunSystem</c>). In License mode, interacting with an item
/// that has <see cref="ShipLicenseRequiredComponent"/> begins a two-click confirm flow which
/// withdraws the licensing fee from the user's bank and stamps a <see cref="ShipLicenseComponent"/>
/// bound to the ship whose deed was held when the mode was activated.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipLicensingToolComponent : Component
{
    [DataField, AutoNetworkedField]
    public ShipLicensingToolMode Mode = ShipLicensingToolMode.Appraise;

    /// <summary>Ship the tool is currently bound to (captured when entering License mode).</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? BoundShuttleUid;

    [DataField, AutoNetworkedField]
    public string? BoundShuttleName;

    /// <summary>Server-side: target awaiting confirm.</summary>
    [DataField]
    public EntityUid? PendingTarget;

    [DataField]
    public int PendingFee;

    [DataField]
    public TimeSpan PendingExpires;

    /// <summary>How long after a price-quote click the second confirm click is accepted.</summary>
    [DataField]
    public TimeSpan ConfirmWindow = TimeSpan.FromSeconds(6);

    [DataField]
    public SoundSpecifier StampSound = new SoundPathSpecifier("/Audio/Items/appraiser.ogg");
}
