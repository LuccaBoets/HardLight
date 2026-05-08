using System.Numerics;
using Content.Server.GameTicking.Rules;
using Content.Server.Parallax;
using Content.Server.Station.Systems;
using Content.Shared._VRS.Planet;
using Content.Shared.GameTicking.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._VRS.Planet;

/// <summary>
/// Spawns a single persistent biome planet at round start, wired up with the
/// Landgrab plot registry, the procedural dungeon + ambient mob spawner, and
/// an FTL beacon so any shuttle console picks it up automatically.
/// </summary>
public sealed class VRSPersistentPlanetRuleSystem : GameRuleSystem<VRSPersistentPlanetRuleComponent>
{
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedShuttleSystem _shuttle = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Beacon prototype used to make the planet appear in the shuttle console
    /// destination list. Defined in <c>Resources/Prototypes/Entities/Markers/shuttle.yml</c>.
    /// </summary>
    private const string FtlBeaconProto = "FTLPoint";

    protected override void Started(EntityUid uid, VRSPersistentPlanetRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (component.SpawnedPlanet != null)
            return; // idempotent — never spawn twice if the rule restarts

        if (!_proto.TryIndex(component.BiomeTemplate, out var template))
        {
            Log.Error($"VRSPersistentPlanet: biome template '{component.BiomeTemplate}' not found.");
            return;
        }

        // Fresh map for the planet. Biome chunks are generated lazily as
        // players approach, so the map starts effectively empty.
        _map.CreateMap(out var mapId);
        var mapUid = _mapManager.GetMapEntityId(mapId);

        var seed = component.Seed ?? _random.Next();
        _biome.EnsurePlanet(mapUid, template, seed);

        // Pick a name and stamp it everywhere players will see it.
        var name = component.NamePool.Count > 0
            ? _random.Pick(component.NamePool)
            : "Frontier";
        _meta.SetEntityName(mapUid, name);

        // Plot registry — enables the Landgrab cartridge to operate here.
        var registry = EnsureComp<PlanetPlotRegistryComponent>(mapUid);
        registry.PlanetName = name;
        registry.PurchaseCost = component.PurchaseCost;
        Dirty(mapUid, registry);

        // Ambient procedural content (wandering mob marker layers + dungeon rolls).
        EnsureComp<PlanetSpawnerComponent>(mapUid);

        // IFF so the planet is distinguishable on the radar / shuttle map.
        // SetIFFColor handles Dirty + access permissions.
        EnsureComp<Content.Shared.Shuttles.Components.IFFComponent>(mapUid);
        _shuttle.SetIFFColor(mapUid, component.IffColor);

        // FTL beacon at the origin so the shuttle console GetBeacons() loop
        // finds the planet and surfaces it as a selectable destination.
        var beaconCoords = new EntityCoordinates(mapUid, Vector2.Zero);
        var beacon = Spawn(FtlBeaconProto, beaconCoords);
        _meta.SetEntityName(beacon, name);

        component.SpawnedPlanet = mapUid;
        Log.Info($"VRSPersistentPlanet '{name}' spawned (map {mapId}, biome {template.ID}, seed {seed}).");
    }
}
