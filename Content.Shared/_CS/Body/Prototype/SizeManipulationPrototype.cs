using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Prototype;

/// <summary>
/// Describes the effect a certain size can have on an entity.
/// </summary>
[Prototype("sizeManipulation")]
public sealed partial class SizeManipulationPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Lower scale range for the effects to be applied
    /// If the scael is between MinScale and MaxScale the Components get applied
    /// </summary>
    [DataField]
    public float MinScale = 1f - 0.15f;

    /// <summary>
    /// Upper scale range for the effects to be applied
    /// If the scael is between MinScale and MaxScale the Components get applied
    /// </summary>
    [DataField]
    public float MaxScale = 1f + 0.15f;

    /// <summary>
    /// The components that get added to the player, when they gain that size.
    /// </summary>
    [DataField]
    public ComponentRegistry Components { get; private set; } = new();
}
