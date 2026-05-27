using Robust.Shared.GameStates;

namespace Content.Shared._VRS.Licensing;

/// <summary>
/// Marker placed on items that must be licensed to a specific ship before they can be anchored
/// on a non-station grid. Without a matching <see cref="ShipLicenseComponent"/>, the anchor attempt
/// is cancelled. Intended for high-value or expedition-recovered objects to prevent stockpiling.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShipLicenseRequiredComponent : Component
{
    /// <summary>
    /// Fee fraction override for this item. If null, falls back to the
    /// <c>vrs.licensing.fee_fraction</c> CVar.
    /// </summary>
    [DataField]
    public float? FeeFractionOverride;

    /// <summary>
    /// Minimum flat fee charged for licensing, regardless of appraisal-derived fee.
    /// </summary>
    [DataField]
    public int MinimumFee = 250;
}
