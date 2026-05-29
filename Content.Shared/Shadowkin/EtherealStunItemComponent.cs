namespace Content.Shared.Shadowkin;

/// <summary>
/// Shared component to configure an ethereal stun item's behavior.
/// </summary>
[RegisterComponent]
public sealed partial class EtherealStunItemComponent : Component
{
    /// <summary>
    /// Radius of the stun effect, in tiles.
    /// </summary>
    [DataField]
    public float Radius = 10f;

    /// <summary>
    /// If true, the item is deleted after use.
    /// </summary>
    [DataField]
    public bool DeleteOnUse = true;
}