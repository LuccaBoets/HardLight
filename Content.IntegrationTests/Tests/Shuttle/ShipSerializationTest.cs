using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Shuttles.Save;
using Content.Tests;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Light.Components;
using Content.Shared.Shuttles.Save;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
#nullable enable
namespace Content.IntegrationTests.Tests.Shuttle;

/// <summary>
/// Regression test: ensure the refactored ShipSerializationSystem actually serializes entities
/// (previously only tiles were saved due to incorrect YAML parsing).
/// </summary>
public sealed class ShipSerializationTest : ContentUnitTest
{
    private static readonly string[] ActionComponentTypes =
    {
        nameof(InstantActionComponent),
        nameof(EntityTargetActionComponent),
        nameof(WorldTargetActionComponent),
        nameof(EntityWorldTargetActionComponent),
    };

    [Test]
    public async Task RefactoredSerializer_SerializesEntities()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var map = await pair.CreateTestMap();

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var shipSer = entManager.System<ShipSerializationSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapSys = entManager.System<SharedMapSystem>();
        const string shipName = "TestShip";

        await server.WaitAssertion(() =>
        {
            // Ensure we use the refactored path.
            cfg.SetCVar(CCVars.ShipyardUseLegacySerializer, false);

            // Create a fresh grid separate from default test map grid (remove initial grid to minimize noise).
            entManager.DeleteEntity(map.Grid);
            var gridEnt = mapManager.CreateGridEntity(map.MapId);
            var gridUid = gridEnt.Owner;
            var gridComp = gridEnt.Comp;

            entManager.RunMapInit(gridUid, entManager.GetComponent<MetaDataComponent>(gridUid));

            // Lay down tiles so spawned entities can anchor if needed.
            mapSys.SetTile(gridUid, gridComp, Vector2i.Zero, new Tile(1));
            mapSys.SetTile(gridUid, gridComp, new Vector2i(1, 0), new Tile(1));

            // Spawn a couple of simple prototypes that should serialize (avoid ones filtered like vending machines).
            var coords = new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f));
            var ent1 = entManager.SpawnEntity("AirlockShuttle", coords);
            var ent2 = entManager.SpawnEntity("ChairBrass", new EntityCoordinates(gridUid, new Vector2(1.5f, 0.5f)));

            Assert.Multiple(() =>
            {
                // Sanity: they exist and are children of the grid.
                Assert.That(entManager.EntityExists(ent1));
                Assert.That(entManager.EntityExists(ent2));
                Assert.That(entManager.GetComponent<TransformComponent>(ent1).ParentUid, Is.EqualTo(gridUid));
                Assert.That(entManager.GetComponent<TransformComponent>(ent2).ParentUid, Is.EqualTo(gridUid));
            });

            var playerId = new NetUserId(Guid.NewGuid());
            var data = shipSer.SerializeShip(gridUid, playerId, shipName);

            Assert.That(data.Grids, Has.Count.EqualTo(1), "Expected exactly one grid serialized");
            var g = data.Grids[0];

            Assert.Multiple(() =>
            {
                // Tiles: we placed exactly two non-space tiles.
                Assert.That(g.Tiles, Has.Count.EqualTo(2), "Expected two non-space tiles");

                // Entities: expect at least the two we spawned, though additional infrastructure entities (grid, etc.) may appear.
                // We only store entities with valid prototypes; ensure count >=2 and contains our prototypes.
                Assert.That(g.Entities, Has.Count.GreaterThanOrEqualTo(2), $"Expected at least 2 entities, got {g.Entities.Count}");
            });

            var protos = g.Entities.Select(e => e.Prototype).ToHashSet();
            Assert.Multiple(() =>
            {
                Assert.That(protos, Does.Contain("AirlockShuttle"), "Serialized entities missing AirlockShuttle prototype");
                Assert.That(protos, Does.Contain("ChairBrass"), "Serialized entities missing ChairBrass prototype");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RefactoredSerializer_RebuildsRuntimeActionsOnLoad()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var sourceMap = await pair.CreateTestMap();
        var targetMap = await pair.CreateTestMap();

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var shipSer = entManager.System<ShipSerializationSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapSys = entManager.System<SharedMapSystem>();
        var xformSys = entManager.System<SharedTransformSystem>();
        EntityUid sourceGridUid = default;
        EntityUid flashlight = default;
        ShipGridData data = null!;
        EntityUid restoredGrid = default;

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCVars.ShipyardUseLegacySerializer, false);

            entManager.DeleteEntity(sourceMap.Grid);
            var sourceGrid = mapManager.CreateGridEntity(sourceMap.MapId);
            sourceGridUid = sourceGrid.Owner;
            var sourceGridComp = sourceGrid.Comp;

            entManager.RunMapInit(sourceGridUid, entManager.GetComponent<MetaDataComponent>(sourceGridUid));
            mapSys.SetTile(sourceGridUid, sourceGridComp, Vector2i.Zero, new Tile(1));

            flashlight = entManager.SpawnEntity("FlashlightLantern", new EntityCoordinates(sourceGridUid, new Vector2(0.5f, 0.5f)));
            Assert.That(xformSys.AnchorEntity(flashlight, entManager.GetComponent<TransformComponent>(flashlight)), Is.True);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entManager.TryGetComponent(flashlight, out HandheldLightComponent? lightComp));
            Assert.That(entManager.GetComponent<TransformComponent>(flashlight).ParentUid, Is.EqualTo(sourceGridUid));

            Assert.Multiple(() =>
            {
                Assert.That(lightComp!.ToggleActionEntity, Is.Not.Null);
                Assert.That(lightComp.SelfToggleActionEntity, Is.Not.Null);
            });

            data = shipSer.SerializeShip(sourceGridUid, new NetUserId(Guid.NewGuid()), "ActionShip");
            var gridData = data.Grids.Single();

            Assert.That(gridData.Entities.SelectMany(entity => entity.Components)
                    .Any(component => ActionComponentTypes.Contains(component.Type)),
                Is.False,
                "Ship serialization should not persist generated action entities.");

            Assert.That(gridData.Entities.SelectMany(entity => entity.Components)
                    .Any(component => (component.YamlData?.Contains("toggleActionEntity", StringComparison.OrdinalIgnoreCase) ?? false)
                        || (component.YamlData?.Contains("selfToggleActionEntity", StringComparison.OrdinalIgnoreCase) ?? false)),
                Is.False,
                "Ship serialization should scrub runtime action entity references from component YAML.");

            restoredGrid = shipSer.ReconstructShipOnMap(data, targetMap.MapId, Vector2.Zero);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var foundRestoredLight = false;
            EntityUid restoredLight = default;
            HandheldLightComponent? restoredLightComp = null;
            var lightQuery = entManager.EntityQueryEnumerator<HandheldLightComponent, TransformComponent>();
            while (lightQuery.MoveNext(out var uid, out var comp, out var xform))
            {
                if (xform.GridUid != restoredGrid)
                    continue;

                restoredLight = uid;
                restoredLightComp = comp;
                foundRestoredLight = true;
                break;
            }

            Assert.That(foundRestoredLight, Is.True, "Expected reconstructed ship to contain the flashlight.");
            Assert.That(restoredLightComp, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(restoredLightComp!.ToggleActionEntity, Is.Not.Null);
                Assert.That(restoredLightComp.SelfToggleActionEntity, Is.Not.Null);
                Assert.That(entManager.EntityExists(restoredLightComp.ToggleActionEntity!.Value), Is.True);
                Assert.That(entManager.EntityExists(restoredLightComp.SelfToggleActionEntity!.Value), Is.True);
            });

            var actionCount = 0;
            var actionQuery = entManager.EntityQueryEnumerator<InstantActionComponent, TransformComponent>();
            while (actionQuery.MoveNext(out _, out _, out var xform))
            {
                if (xform.ParentUid == restoredLight)
                    actionCount++;
            }

            Assert.That(actionCount, Is.EqualTo(2), "Reconstructed flashlight should recreate exactly its two runtime actions.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RefactoredSerializer_RestoresSolutionAppearance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var sourceMap = await pair.CreateTestMap();
        var targetMap = await pair.CreateTestMap();

        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var shipSer = entManager.System<ShipSerializationSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapSys = entManager.System<SharedMapSystem>();
        var appearanceSystem = entManager.System<SharedAppearanceSystem>();
        var solutionSystem = entManager.System<SharedSolutionContainerSystem>();
        var xformSys = entManager.System<SharedTransformSystem>();
        EntityUid sourceGridUid = default;
        EntityUid beaker = default;
        float originalFill = 0f;
        ShipGridData data = null!;
        EntityUid restoredGrid = default;

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCVars.ShipyardUseLegacySerializer, false);

            entManager.DeleteEntity(sourceMap.Grid);
            var sourceGrid = mapManager.CreateGridEntity(sourceMap.MapId);
            sourceGridUid = sourceGrid.Owner;
            var sourceGridComp = sourceGrid.Comp;

            entManager.RunMapInit(sourceGridUid, entManager.GetComponent<MetaDataComponent>(sourceGridUid));
            mapSys.SetTile(sourceGridUid, sourceGridComp, Vector2i.Zero, new Tile(1));

            beaker = entManager.SpawnEntity("Beaker", new EntityCoordinates(sourceGridUid, new Vector2(0.5f, 0.5f)));
            Assert.That(xformSys.AnchorEntity(beaker, entManager.GetComponent<TransformComponent>(beaker)), Is.True);
            Assert.That(entManager.TryGetComponent(beaker, out AppearanceComponent? beakerAppearance));
            Assert.That(solutionSystem.TryGetSolution(beaker, "beaker", out var solutionEnt, out var solution));

            solution!.AddSolution(new Solution("Water", FixedPoint2.New(10)), protoManager);
            solutionSystem.UpdateChemicals(solutionEnt!.Value, false);

            Assert.That(appearanceSystem.TryGetData(beaker, SolutionContainerVisuals.FillFraction, out originalFill, beakerAppearance), Is.True);
            Assert.That(originalFill, Is.GreaterThan(0f));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entManager.GetComponent<TransformComponent>(beaker).ParentUid, Is.EqualTo(sourceGridUid));

            data = shipSer.SerializeShip(sourceGridUid, new NetUserId(Guid.NewGuid()), "SolutionShip");
            restoredGrid = shipSer.ReconstructShipOnMap(data, targetMap.MapId, Vector2.Zero);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var foundRestoredBeaker = false;
            AppearanceComponent? restoredAppearance = null;
            EntityUid restoredBeaker = default;
            var beakerQuery = entManager.EntityQueryEnumerator<AppearanceComponent, TransformComponent, SolutionContainerManagerComponent>();
            while (beakerQuery.MoveNext(out var uid, out var appearance, out var xform, out var solutionManager))
            {
                if (xform.GridUid != restoredGrid)
                    continue;

                if (!solutionSystem.TryGetSolution((uid, solutionManager), "beaker", out _, out var restoredCandidate))
                    continue;

                restoredBeaker = uid;
                restoredAppearance = appearance;
                foundRestoredBeaker = true;
                break;
            }

            Assert.That(foundRestoredBeaker, Is.True, "Expected reconstructed ship to contain the beaker.");
            Assert.That(restoredAppearance, Is.Not.Null);
            Assert.That(appearanceSystem.TryGetData(restoredBeaker, SolutionContainerVisuals.FillFraction, out float restoredFill, restoredAppearance), Is.True);
            Assert.That(restoredFill, Is.EqualTo(originalFill).Within(0.001f), "Restored beaker should retain its fill-level appearance data.");

            Assert.That(solutionSystem.TryGetSolution(restoredBeaker, "beaker", out _, out var restoredSolution));
            Assert.That(restoredSolution!.Volume.Float(), Is.EqualTo(10f).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Coverage: the async ShipLoadJob path in <see cref="ShipSerializationSystem.Update"/> must
    /// (a) actually defer work past the call to ReconstructShipOnMap when async is on, and
    /// (b) eventually restore every entity that was serialized. A regression in either direction
    /// silently breaks player ship loading on production: (a) means the load tick still pays the
    /// full cost (perf regression); (b) means players get a half-loaded ship (correctness regression).
    /// We force tiny per-tick batches so completion provably spans multiple ticks even on a small
    /// test grid.
    /// </summary>
    [Test]
    public async Task RefactoredSerializer_AsyncLoaderCompletesAcrossTicks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var sourceMap = await pair.CreateTestMap();
        var targetMap = await pair.CreateTestMap();

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var shipSer = entManager.System<ShipSerializationSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapSys = entManager.System<SharedMapSystem>();

        const int spawnCount = 20;
        EntityUid sourceGridUid = default;
        ShipGridData data = null!;
        EntityUid restoredGrid = default;

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCVars.ShipyardUseLegacySerializer, false);
            // Force the async path on (default true, but make the test self-contained against
            // future default flips) and squeeze the per-tick batch so we are guaranteed to need
            // multiple ticks to drain spawnCount entities. The time budget stays generous so the
            // test isn't sensitive to host CPU jitter.
            cfg.SetCVar(Content.Shared.HL.CCVar.HLCCVars.ShipLoadAsync, true);
            cfg.SetCVar(Content.Shared.HL.CCVar.HLCCVars.ShipLoadBatchNonContained, 4);
            cfg.SetCVar(Content.Shared.HL.CCVar.HLCCVars.ShipLoadBatchContained, 4);
            cfg.SetCVar(Content.Shared.HL.CCVar.HLCCVars.ShipLoadTimeBudgetMs, 1000);

            entManager.DeleteEntity(sourceMap.Grid);
            var sourceGrid = mapManager.CreateGridEntity(sourceMap.MapId);
            sourceGridUid = sourceGrid.Owner;
            var sourceGridComp = sourceGrid.Comp;

            entManager.RunMapInit(sourceGridUid, entManager.GetComponent<MetaDataComponent>(sourceGridUid));

            // Lay down a row of tiles and spawn a chair on each. ChairBrass is a cheap, fully
            // serializable prototype already exercised by RefactoredSerializer_SerializesEntities.
            for (var i = 0; i < spawnCount; i++)
            {
                mapSys.SetTile(sourceGridUid, sourceGridComp, new Vector2i(i, 0), new Tile(1));
                entManager.SpawnEntity("ChairBrass",
                    new EntityCoordinates(sourceGridUid, new Vector2(i + 0.5f, 0.5f)));
            }
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            data = shipSer.SerializeShip(sourceGridUid, new NetUserId(Guid.NewGuid()), "BatchShip");
            Assert.That(data.Grids, Has.Count.EqualTo(1));
            Assert.That(data.Grids[0].Entities, Has.Count.GreaterThanOrEqualTo(spawnCount),
                $"Expected at least {spawnCount} entities serialized, got {data.Grids[0].Entities.Count}");

            restoredGrid = shipSer.ReconstructShipOnMap(data, targetMap.MapId, Vector2.Zero);
            Assert.That(entManager.EntityExists(restoredGrid), Is.True,
                "ReconstructShipOnMap should return a live grid even when async loading is enabled.");
            // ReconstructShipOnMap queues a delayed `fixgridatmos` console command (see
            // ShipSerializationSystem.ApplyFixGridAtmosphereToGrid). On a real production load
            // the YAML carries a GridAtmosphereComponent so the command succeeds; on a synthetic
            // test grid it doesn't, and the resulting Sawmill.Error trips the integration test
            // log handler. Attach atmosphere here so the timer fires cleanly.
            entManager.AddComponent<GridAtmosphereComponent>(restoredGrid);
        });

        // Run one tick. With ShipLoadBatchNonContained=4 and >=spawnCount entities to spawn, the
        // loader should have NOT yet finished. This proves the async path is actually deferring
        // work; if a regression made ReconstructShipOnMap synchronous all spawnCount chairs would
        // already exist after this single tick.
        await server.WaitRunTicks(1);
        int afterFirstTick = 0;
        await server.WaitAssertion(() =>
        {
            afterFirstTick = CountChairsOnGrid(entManager, restoredGrid);
            Assert.That(afterFirstTick, Is.LessThan(spawnCount),
                $"Async loader should not finish in one tick with batch size 4 (saw {afterFirstTick}/{spawnCount}). "
                + "If this fails, the async deferral was bypassed and the load tick is paying full cost.");
        });

        // Now run enough ticks to comfortably drain the queue (spawnCount/batch + slack).
        await server.WaitRunTicks(2 + spawnCount / 4 + 5);

        await server.WaitAssertion(() =>
        {
            var afterDrain = CountChairsOnGrid(entManager, restoredGrid);
            Assert.That(afterDrain, Is.EqualTo(spawnCount),
                $"Async loader should restore every serialized chair (got {afterDrain}/{spawnCount}). "
                + "If this fails, ShipLoadJob completion is dropping entities mid-batch.");
            Assert.That(afterDrain, Is.GreaterThan(afterFirstTick),
                "Sanity: more chairs should be present after draining than after the first tick.");
        });

        await pair.CleanReturnAsync();

        static int CountChairsOnGrid(IEntityManager em, EntityUid grid)
        {
            var count = 0;
            var query = em.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out _, out var meta, out var xform))
            {
                if (xform.GridUid != grid)
                    continue;
                if (meta.EntityPrototype?.ID == "ChairBrass")
                    count++;
            }

            return count;
        }
    }
}
