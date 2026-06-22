using Content.Shared.Storage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Animals.Systems;

namespace Content.Shared.Animals.Components; // Moved this to Shared so the client can use it for verb drawing.

/// <summary>
///     This component handles prizeball laying for the prizeball layer trait
/// </summary>

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class PrizeballLayingComponent : Component
{
    [DataField]
    public EntProtoId ActionPrototype = "ActionLayPrizeball";

    [DataField]
    public EntityUid? Action;

    /// <summary>
    ///     Messages while producing prizeballs
    /// </summary>
    [DataField]
    public IReadOnlyList<string> FlavorMessages = new[]
    {
        "action-popup-lay-pball-flavor-1",
        "action-popup-lay-pball-flavor-2",
        "action-popup-lay-pball-flavor-3",
        "action-popup-lay-pball-flavor-4"
    };

    /// <summary>
    ///     The item that gets laid/spawned, retrieved from animal prototype.
    /// </summary>
    [DataField(required: true)]
    public List<EntitySpawnEntry> EggSpawn = new();

    /// <summary>
    ///     The sound played when prizeball pops out
    /// </summary>
    [DataField]
    public SoundSpecifier PballLaySound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");

    /// <summary>
    ///     How many prizeballs produced per unit of cum
    /// </summary>
    [DataField]
    public float ProductionMult = 0.2f;

    /// <summary>
    ///     How many prizeballs between each flavor text
    /// </summary>
    [DataField]
    public float FlavorFreq = 6.0f;

    /// <summary>
    ///     The number of pballs when movespeed is slowed
    /// </summary>
    [DataField]
    public float PballSlowThreshold = 10;

    /// <summary>
    ///     The max number of prizeballs you can hold
    /// </summary>
    [DataField]
    public float MaxPballs = 24;

    /// <summary>
    ///     How much the user is slowed by prizeballs
    /// </summary>
    [DataField]
    public float PballSlowMult = 0.5f;

    /// <summary>
    ///     How long it takes for the prizeball to come out
    /// </summary>
    [DataField]
    public float PballLayDelay = 5.0f;

    /// <summary>
    /// The number of prizeballs in your belly
    /// </summary>
    [DataField, AutoNetworkedField]
    public float pballs = 0;

    /// <summary>
    /// The number of prizeballs produced since last flavor text
    /// </summary>
    public float pballsFlavorAccum = 0;

    /// <summary>
    /// The number of prizeballs produced since last flavor text
    /// </summary>
    public bool Temporary = false;
    public bool hasPballs()
    {
        return pballs >= 1.0f;
    }
    public bool isHeavyOfPballs()
    {
        return pballs >= PballSlowThreshold;
    }
    public bool isFullOfPballs()
    {
        return pballs >= MaxPballs;
    }
    public bool doFlavor()
    {
        if(pballsFlavorAccum >= FlavorFreq)
        {
            pballsFlavorAccum -= FlavorFreq;
            return true;
        }
        return false;
    }
    public void makeTempFrom(PrizeballLayingComponent other)
    {
        FlavorMessages = other.FlavorMessages;
        EggSpawn = other.EggSpawn;
        PballLaySound = other.PballLaySound;
        PballSlowThreshold = other.PballSlowThreshold;
        MaxPballs = other.MaxPballs;
        PballSlowMult = other.PballSlowMult;
        PballLayDelay = other.PballLayDelay;
        Temporary = true;
    }
}