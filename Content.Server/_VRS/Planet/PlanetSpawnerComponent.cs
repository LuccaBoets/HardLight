using System.Numerics;
using Content.Shared.Parallax.Biomes.Markers;
using Content.Shared.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Server._VRS.Planet;

/// <summary>
/// Drives ambient world-population on a persistent planet:
/// <list type="bullet">
///   <item>Adds a configured set of <see cref="BiomeMarkerLayerPrototype"/>s
///         on map init so wandering mob packs spawn naturally as players
///         explore (Poisson-disc spacing handled by the marker system).</item>
///   <item>Periodically rolls to procedurally spawn dungeons in regions
///         that are clear of player plots, shuttles and previously-spawned
///         dungeons.</item>
/// </list>
/// Place this on the planet's map entity alongside <see cref="PlanetPlotRegistryComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class PlanetSpawnerComponent : Component
{
    /// <summary>
    /// Wandering-mob marker layers added to the planet's <see cref="BiomeComponent"/> on map init.
    /// </summary>
    [DataField]
    public List<ProtoId<BiomeMarkerLayerPrototype>> WanderingMobLayers = new()
    {
        "VRSWanderingXenos",
        "VRSWanderingXenoDrones",
        "VRSWanderingXenoRunners",
        "VRSWanderingExplorersMelee",
        "VRSWanderingExplorersRanged",
        "VRSWanderingCarpsSalvage",
    };

    /// <summary>
    /// Pool of dungeon configurations the spawner can roll. Reuses the same
    /// configs salvage expeditions use.
    /// </summary>
    [DataField]
    public List<ProtoId<DungeonConfigPrototype>> DungeonConfigs = new()
    {
        "NFExperiment",
        "NFHaunted",
        "NFLavaBrig",
        "NFMineshaft",
        "NFSnowyLabs",
        "NFCaveFactory",
        "NFMedSci",
        "NFFactoryDorms",
    };

    /// <summary>
    /// How often a dungeon-spawn roll is attempted. A longer interval combined
    /// with a higher per-roll chance gives the same expected spawn cadence as a
    /// short interval + low chance, but with far fewer Update wakeups and
    /// far fewer wasted exclusion scans.
    /// </summary>
    [DataField]
    public TimeSpan RollInterval = TimeSpan.FromMinutes(5);

    /// <summary>Probability per roll of *attempting* placement (capped by exclusions).</summary>
    [DataField]
    public float SpawnChancePerRoll = 1.0f;

    /// <summary>
    /// Hard ceiling so the planet doesn't get carpet-bombed. Each spawned
    /// dungeon permanently inflates the planet grid's entity count, so this
    /// directly bounds long-session memory growth.
    /// </summary>
    [DataField]
    public int MaxDungeons = 6;

    /// <summary>Minimum world-tile distance from any player plot or shuttle.</summary>
    [DataField]
    public float MinDistFromActivity = 300f;

    /// <summary>Minimum world-tile distance between any two spawned dungeons.</summary>
    [DataField]
    public float MinDistBetweenDungeons = 600f;

    /// <summary>
    /// Radius (world tiles) checked around an existing dungeon to decide if it
    /// has been "cleared". Dungeons with no live AI mobs inside this radius are
    /// pruned from the spawn list before the cap check, allowing fresh ones to
    /// spawn elsewhere on the planet. Set roughly to the dungeon footprint plus
    /// the wandering-mob leash distance.
    /// </summary>
    [DataField]
    public float DungeonClearanceRadius = 600f;

    /// <summary>Set true once <see cref="WanderingMobLayers"/> have been registered.</summary>
    [ViewVariables]
    public bool MarkerLayersAdded;

    /// <summary>Server time of the next dungeon-spawn roll.</summary>
    [ViewVariables]
    public TimeSpan NextRoll;

    /// <summary>World positions of dungeons we've already placed this round.</summary>
    [ViewVariables]
    public List<Vector2> SpawnedDungeons = new();
}
