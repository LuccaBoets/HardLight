namespace Content.Shared._Starlight.NullSpace;

/// <summary>
/// Marker added to a carried entity when its NullSpace carrier grants it pressure immunity.
/// Prevents us from stripping pre-existing pressure immunity the carried entity already had.
/// </summary>
[RegisterComponent]
public sealed partial class NullCarryPressureImmunityComponent : Component { }
