using Content.Shared.EntityEffects;
using Content.Shared.Random;
using Content.Shared.Traits;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.Body.Systems;

/// <summary>
/// Describes the effect a certain size can have on an entity.
/// </summary>
[Prototype]
public sealed partial class SizeManipulationPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float MinScale = 1f - 0.15f;

    [DataField]
    public float MaxScale = 1f + 0.15f;

    /// <summary>
    /// The components that get added to the player, when they gain that size.
    /// </summary>
    [DataField]
    public ComponentRegistry Components { get; private set; } = new();
}
