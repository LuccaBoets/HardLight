using System.Linq;
using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Server.Shuttles.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Procedural;
using Content.Shared.Shuttles.Components;
using Content.Shared._VRS.Planet;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._VRS.Planet;

/// <summary>
/// Drives the <see cref="PlanetSpawnerComponent"/>: registers ambient mob
/// marker layers on map init and periodically rolls dungeon placements in
/// regions clear of player activity.
/// </summary>
public sealed class PlanetSpawnerSystem : EntitySystem
{
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly DungeonSystem _dungeon = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>
    /// Beacon prototype spawned at each dungeon center so the shuttle console
    /// surfaces them as selectable FTL destinations alongside the planet itself.
    /// </summary>
    private const string DungeonBeaconProto = "FTLPoint";

    private ISawmill _sawmill = default!;

    /// <summary>
    /// Biome chunks are 8 tiles. We sample candidate dungeon centers on a
    /// coarser grid so adjacent rolls don't keep proposing micro-overlapping
    /// positions.
    /// </summary>
    private const int CandidateStride = 32;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("planet.spawner");
        SubscribeLocalEvent<PlanetSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<PlanetSpawnerComponent> ent, ref MapInitEvent args)
    {
        EnsureMarkerLayers(ent);
        EnsurePreseeded(ent);
        ent.Comp.NextRoll = _timing.CurTime + ent.Comp.RollInterval;
        ent.Comp.RampStart = _timing.CurTime;
        ent.Comp.NextRampSpawn = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.RampStartIntervalSeconds);
    }

    /// <summary>
    /// Pre-seeds <see cref="PlanetSpawnerComponent.PreseedDungeonCount"/> dungeons
    /// at random offsets from the planet origin in a configurable ring. Submits
    /// all jobs in one go; the dungeon system processes them off the hot path.
    /// </summary>
    private void EnsurePreseeded(Entity<PlanetSpawnerComponent> ent)
    {
        if (ent.Comp.Preseeded)
            return;
        if (!TryComp<MapGridComponent>(ent.Owner, out var grid))
            return;

        var registry = EnsureComp<PlanetDungeonRegistryComponent>(ent.Owner);

        for (var i = 0; i < ent.Comp.PreseedDungeonCount; i++)
        {
            var sliceAngle = (i + _random.NextFloat()) * (MathF.Tau / Math.Max(1, ent.Comp.PreseedDungeonCount));
            var radius = _random.NextFloat(ent.Comp.PreseedRingMin, ent.Comp.PreseedRingMax);

            var x = MathF.Cos(sliceAngle) * radius;
            var y = MathF.Sin(sliceAngle) * radius;

            var snappedTile = new Vector2i(
                (int)MathF.Round(x / CandidateStride) * CandidateStride,
                (int)MathF.Round(y / CandidateStride) * CandidateStride);

            var configId = _random.Pick(ent.Comp.DungeonConfigs);
            if (!_proto.TryIndex(configId, out var config))
            {
                _sawmill.Warning($"PlanetSpawner missing dungeon config '{configId}' (preseed).");
                continue;
            }

            var seed = HashCode.Combine(_timing.CurTime.Ticks, snappedTile.X, snappedTile.Y, i);
            _dungeon.GenerateDungeon(config, configId.Id, ent.Owner, grid, snappedTile, seed);
            ent.Comp.SpawnedDungeons.Add(new Vector2(snappedTile.X, snappedTile.Y));

            var beaconCoords = new EntityCoordinates(ent.Owner, new Vector2(snappedTile.X, snappedTile.Y));
            var beacon = Spawn(DungeonBeaconProto, beaconCoords);
            _meta.SetEntityName(beacon, configId.Id);

            registry.Dungeons.Add(new DungeonMarker(new Vector2(snappedTile.X, snappedTile.Y), configId.Id));
            _sawmill.Info($"Pre-seeded planet dungeon {i + 1}/{ent.Comp.PreseedDungeonCount} '{configId}' at ({snappedTile.X}, {snappedTile.Y}) on {ToPrettyString(ent.Owner)}.");

            PopulateDungeonGarrison(ent, snappedTile);
        }

        Dirty(ent.Owner, registry);
        ent.Comp.Preseeded = true;
    }

    /// <summary>
    /// Spawns a baseline garrison of mobs in a ring around a freshly-placed
    /// dungeon. Counts and ring sizes come from
    /// <see cref="PlanetSpawnerComponent.PreseedMobsPerDungeon"/> and friends.
    /// </summary>
    private void PopulateDungeonGarrison(Entity<PlanetSpawnerComponent> ent, Vector2i centerTile)
    {
        if (ent.Comp.PreseedMobsPerDungeon <= 0 || ent.Comp.RampingMobs.Count == 0)
            return;
        if (!TryComp<TransformComponent>(ent.Owner, out _))
            return;

        var center = new Vector2(centerTile.X, centerTile.Y);
        for (var i = 0; i < ent.Comp.PreseedMobsPerDungeon; i++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var dist = _random.NextFloat(ent.Comp.PreseedMobInnerRadius, ent.Comp.PreseedMobOuterRadius);
            var offset = new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

            var coords = new EntityCoordinates(ent.Owner, center + offset);
            var proto = _random.Pick(ent.Comp.RampingMobs);
            Spawn(proto, coords);
        }
    }

    private void EnsureMarkerLayers(Entity<PlanetSpawnerComponent> ent)
    {
        if (ent.Comp.MarkerLayersAdded)
            return;
        if (!TryComp<BiomeComponent>(ent.Owner, out var biome))
            return;

        foreach (var layerId in ent.Comp.WanderingMobLayers)
        {
            if (!_proto.HasIndex(layerId))
            {
                _sawmill.Warning($"PlanetSpawner on {ToPrettyString(ent.Owner)} references missing marker layer '{layerId}'.");
                continue;
            }
            // BiomeComponent.MarkerLayers is access-restricted; rely on the
            // MarkerLayersAdded flag (set below) as the dedupe guard.
            _biome.AddMarkerLayer(ent.Owner, biome, layerId.Id);
        }
        ent.Comp.MarkerLayersAdded = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<PlanetSpawnerComponent>();
        while (query.MoveNext(out var uid, out var spawner))
        {
            // Re-attempt marker layer registration if biome wasn't ready at MapInit
            // (e.g. when added at runtime via admin tooling).
            if (!spawner.MarkerLayersAdded)
                EnsureMarkerLayers((uid, spawner));

            // Fallback in case MapInit didn't fire for this component (e.g.
            // EnsureComp added after the map was already initialised by the rule).
            if (!spawner.Preseeded)
                EnsurePreseeded((uid, spawner));

            // Time-scaled ambient mob spawns. Independent of the dungeon roll
            // cadence — fires on its own ramping cooldown.
            if (spawner.RampStart == TimeSpan.Zero)
                spawner.RampStart = now;
            if (now >= spawner.NextRampSpawn)
            {
                TrySpawnRampMob((uid, spawner), now);
                spawner.NextRampSpawn = now + TimeSpan.FromSeconds(GetRampInterval(spawner, now));
            }

            if (now < spawner.NextRoll)
                continue;
            spawner.NextRoll = now + spawner.RollInterval;

            // Drop dungeons that no longer have any live AI mobs in their
            // clearance radius. Once nothing is wandering or shooting at the
            // structure it stops costing meaningful per-tick CPU, so we let
            // the spawner replace it elsewhere on the planet.
            PruneClearedDungeons((uid, spawner));

            // Cheap guards before any candidate work:
            if (spawner.SpawnedDungeons.Count >= spawner.MaxDungeons)
                continue; // hard cap reached, never spend cycles searching
            if (_random.NextFloat() > spawner.SpawnChancePerRoll)
                continue;

            TryRollDungeon((uid, spawner));
        }
    }

    /// <summary>
    /// Picks a candidate position from the planet's loaded biome chunks,
    /// validates exclusions, and (if all checks pass) generates one dungeon.
    /// At most one dungeon per call to keep the dungeon job queue from spiking.
    /// </summary>
    private void TryRollDungeon(Entity<PlanetSpawnerComponent> ent)
    {
        if (!TryComp<BiomeComponent>(ent.Owner, out var biome))
            return;
        if (!TryComp<MapGridComponent>(ent.Owner, out var grid))
            return; // planet must be a biome-on-grid (matches salvage pattern)
        if (!TryComp<TransformComponent>(ent.Owner, out var xform))
            return;

        var loadedChunks = biome.LoadedChunks;
        if (loadedChunks.Count == 0)
            return; // nobody is exploring; nothing to gate against and nothing to populate

        // Snapshot exclusion sources ONCE per roll. The original implementation
        // re-ran two EntityQueryEnumerators inside the per-candidate loop,
        // duplicating O(plots + shuttles) component lookups for every one of the
        // 12 sampled chunks. Snapshotting collapses that to a single pass.
        var (plotPositions, shuttlePositions) = SnapshotExclusions(xform.MapID);

        // Pull a small sample of loaded chunks instead of scanning all, so a
        // huge planet doesn't make every roll expensive.
        var sample = SampleLoadedChunks(loadedChunks, 12);

        foreach (var chunk in sample)
        {
            // Center of the chunk in world tiles. Biome chunk size is 8.
            var worldPos = new Vector2(chunk.X + 4f, chunk.Y + 4f);

            if (!IsCandidatePositionClear(worldPos, ent.Comp, plotPositions, shuttlePositions))
                continue;

            // Snap to candidate stride so we don't keep proposing
            // sub-tile-different positions on the same chunk roll-after-roll.
            var snappedTile = new Vector2i(
                (int)MathF.Round(worldPos.X / CandidateStride) * CandidateStride,
                (int)MathF.Round(worldPos.Y / CandidateStride) * CandidateStride);

            var configId = _random.Pick(ent.Comp.DungeonConfigs);
            if (!_proto.TryIndex(configId, out var config))
            {
                _sawmill.Warning($"PlanetSpawner missing dungeon config '{configId}'.");
                continue;
            }

            // Seed combines time + position so re-rolls don't produce identical layouts.
            var seed = HashCode.Combine(_timing.CurTime.Ticks, snappedTile.X, snappedTile.Y);

            _dungeon.GenerateDungeon(config, configId.Id, ent.Owner, grid, snappedTile, seed);
            ent.Comp.SpawnedDungeons.Add(new Vector2(snappedTile.X, snappedTile.Y));

            // Spawn an FTL beacon at the dungeon center so it appears in the
            // shuttle console destination list (the GetBeacons enumerator on
            // SharedShuttleSystem is what populates the MAP tab sidebar).
            var beaconCoords = new EntityCoordinates(ent.Owner, new Vector2(snappedTile.X, snappedTile.Y));
            var beacon = Spawn(DungeonBeaconProto, beaconCoords);
            _meta.SetEntityName(beacon, configId.Id);

            // Mirror to the networked registry so clients (shuttle console map
            // preview) can draw dungeon markers even when the biome chunks
            // containing the dungeon haven't been loaded by that client.
            var registry = EnsureComp<PlanetDungeonRegistryComponent>(ent.Owner);
            registry.Dungeons.Add(new DungeonMarker(new Vector2(snappedTile.X, snappedTile.Y), configId.Id));
            Dirty(ent.Owner, registry);

            _sawmill.Info($"Spawned planet dungeon '{configId}' at ({snappedTile.X}, {snappedTile.Y}) on {ToPrettyString(ent.Owner)}.");
            return; // one per roll
        }
    }

    /// <summary>
    /// Single-pass collection of exclusion-relevant world positions on a given map,
    /// reused across all candidate chunk checks within one dungeon-spawn roll.
    /// </summary>
    private (List<Vector2> Plots, List<Vector2> Shuttles) SnapshotExclusions(MapId mapId)
    {
        var plots = new List<Vector2>();
        var plotQ = EntityQueryEnumerator<LandgrabPlotComponent, TransformComponent>();
        while (plotQ.MoveNext(out var plotUid, out _, out var plotXform))
        {
            if (plotXform.MapID != mapId)
                continue;
            plots.Add(_transform.GetWorldPosition(plotUid));
        }

        var shuttles = new List<Vector2>();
        var shuttleQ = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();
        while (shuttleQ.MoveNext(out var shuttleUid, out _, out var shuttleXform))
        {
            if (shuttleXform.MapID != mapId)
                continue;
            // Skip the planet's own grid (it's not a "ship currently on it").
            if (HasComp<BiomeComponent>(shuttleUid))
                continue;
            shuttles.Add(_transform.GetWorldPosition(shuttleUid));
        }

        return (plots, shuttles);
    }

    /// <summary>
    /// Removes dungeons whose clearance radius contains no live AI mobs
    /// (HTN-driven, alive). Updates both the spawner's working list and the
    /// networked <see cref="PlanetDungeonRegistryComponent"/> so client
    /// preview markers also disappear once the area is "safe".
    /// </summary>
    private void PruneClearedDungeons(Entity<PlanetSpawnerComponent> ent)
    {
        if (ent.Comp.SpawnedDungeons.Count == 0)
            return;
        if (!TryComp<TransformComponent>(ent.Owner, out var xform))
            return;

        var mapId = xform.MapID;
        var radius = ent.Comp.DungeonClearanceRadius;
        TryComp<PlanetDungeonRegistryComponent>(ent.Owner, out var registry);
        var registryDirty = false;

        // Iterate in reverse so removals don't shift unscanned indices.
        for (var i = ent.Comp.SpawnedDungeons.Count - 1; i >= 0; i--)
        {
            var pos = ent.Comp.SpawnedDungeons[i];
            if (HasLiveAiMobs(new MapCoordinates(pos, mapId), radius))
                continue;

            ent.Comp.SpawnedDungeons.RemoveAt(i);

            if (registry != null)
            {
                // Remove the matching marker (Position-keyed; positions are
                // unique because dungeon spacing is enforced).
                for (var r = registry.Dungeons.Count - 1; r >= 0; r--)
                {
                    if (registry.Dungeons[r].Position == pos)
                    {
                        registry.Dungeons.RemoveAt(r);
                        registryDirty = true;
                        break;
                    }
                }
            }

            _sawmill.Info($"Pruned cleared dungeon at ({pos.X}, {pos.Y}) on {ToPrettyString(ent.Owner)}.");
        }

        if (registryDirty && registry != null)
            Dirty(ent.Owner, registry);
    }

    /// <summary>
    /// Returns true if at least one HTN-driven mob is alive within
    /// <paramref name="radius"/> of <paramref name="center"/>.
    /// </summary>
    private bool HasLiveAiMobs(MapCoordinates center, float radius)
    {
        foreach (var mob in _lookup.GetEntitiesInRange<HTNComponent>(center, radius))
        {
            if (!TryComp<MobStateComponent>(mob, out var state))
                continue;
            if (_mobState.IsAlive(mob, state))
                return true;
        }
        return false;
    }

    private List<Vector2i> SampleLoadedChunks(HashSet<Vector2i> chunks, int max)
    {
        if (chunks.Count <= max)
            return chunks.ToList();

        var pool = chunks.ToArray();
        var result = new List<Vector2i>(max);
        for (var i = 0; i < max; i++)
            result.Add(pool[_random.Next(pool.Length)]);
        return result;
    }

    // ── Ramping ambient mob spawner ──────────────────────────────────────────────

    /// <summary>
    /// Returns the current ramp progress in [0, 1] based on elapsed time since
    /// <see cref="PlanetSpawnerComponent.RampStart"/>.
    /// </summary>
    private static float GetRampProgress(PlanetSpawnerComponent spawner, TimeSpan now)
    {
        var peak = MathF.Max(1f, spawner.RampPeakMinutes * 60f);
        var elapsed = (float)(now - spawner.RampStart).TotalSeconds;
        return Math.Clamp(elapsed / peak, 0f, 1f);
    }

    /// <summary>
    /// Linearly interpolates the spawn interval from
    /// <see cref="PlanetSpawnerComponent.RampStartIntervalSeconds"/> down to
    /// <see cref="PlanetSpawnerComponent.RampMinIntervalSeconds"/> as the ramp
    /// progresses.
    /// </summary>
    private static float GetRampInterval(PlanetSpawnerComponent spawner, TimeSpan now)
    {
        var t = GetRampProgress(spawner, now);
        return MathHelper.Lerp(spawner.RampStartIntervalSeconds, spawner.RampMinIntervalSeconds, t);
    }

    /// <summary>
    /// Picks a random player on the planet, picks a random offset around them
    /// inside <see cref="PlanetSpawnerComponent.RampSpawnRadius"/>, and spawns
    /// a small pack of mobs there. The pack size grows linearly with ramp
    /// progress. Skipped silently if there are no players, no live cap headroom,
    /// or no valid candidate.
    /// </summary>
    private void TrySpawnRampMob(Entity<PlanetSpawnerComponent> ent, TimeSpan now)
    {
        // Trim dead/missing entries first so the cap isn't permanently saturated.
        for (var i = ent.Comp.LiveRampMobs.Count - 1; i >= 0; i--)
        {
            var mob = ent.Comp.LiveRampMobs[i];
            if (!Exists(mob))
            {
                ent.Comp.LiveRampMobs.RemoveAt(i);
                continue;
            }
            if (TryComp<MobStateComponent>(mob, out var state) && !_mobState.IsAlive(mob, state))
                ent.Comp.LiveRampMobs.RemoveAt(i);
        }

        if (ent.Comp.LiveRampMobs.Count >= ent.Comp.RampLiveCap)
            return;
        if (ent.Comp.RampingMobs.Count == 0)
            return;
        if (!TryComp<TransformComponent>(ent.Owner, out var planetXform))
            return;

        // Find players currently on this planet map.
        var planetMapId = planetXform.MapID;
        Vector2? anchor = null;
        var anchorCount = 0;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } playerEnt)
                continue;
            if (!TryComp<TransformComponent>(playerEnt, out var pxform) || pxform.MapID != planetMapId)
                continue;

            anchorCount++;
            // Reservoir-style pick so any player on the planet has equal chance.
            if (_random.Next(anchorCount) == 0)
                anchor = _transform.GetWorldPosition(playerEnt);
        }

        if (anchor is not { } anchorPos)
            return;

        // Random point in the ring [RampMinSpawnDistance, RampSpawnRadius] around the player.
        var angle = _random.NextFloat() * MathF.Tau;
        var dist = _random.NextFloat(ent.Comp.RampMinSpawnDistance, ent.Comp.RampSpawnRadius);
        var spawnWorld = anchorPos + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

        var groupCap = ent.Comp.RampStartGroup
            + (int)MathF.Round(ent.Comp.RampMaxExtraGroup * GetRampProgress(ent.Comp, now));
        var groupSize = _random.Next(ent.Comp.RampStartGroup, Math.Max(ent.Comp.RampStartGroup + 1, groupCap + 1));

        var mobProto = _random.Pick(ent.Comp.RampingMobs);
        var room = ent.Comp.RampLiveCap - ent.Comp.LiveRampMobs.Count;
        groupSize = Math.Min(groupSize, room);

        for (var i = 0; i < groupSize; i++)
        {
            // Light per-mob jitter so the pack doesn't stack on one tile.
            var jitter = new Vector2(_random.NextFloat(-1.5f, 1.5f), _random.NextFloat(-1.5f, 1.5f));
            var coords = new MapCoordinates(spawnWorld + jitter, planetMapId);
            var mob = Spawn(mobProto, coords);
            ent.Comp.LiveRampMobs.Add(mob);
        }
    }

    /// <summary>
    /// Returns true when the candidate world-tile position is clear of all
    /// known activity sources: existing dungeons, player plot grids, and
    /// shuttles currently on the planet map. Operates on a pre-collected
    /// snapshot so the same lists are reused across all candidates in a roll.
    /// </summary>
    private static bool IsCandidatePositionClear(Vector2 worldPos, PlanetSpawnerComponent spawner,
        List<Vector2> plotPositions, List<Vector2> shuttlePositions)
    {
        // Dungeon-vs-dungeon spacing.
        var dungeonDistSq = spawner.MinDistBetweenDungeons * spawner.MinDistBetweenDungeons;
        foreach (var existing in spawner.SpawnedDungeons)
        {
            if ((worldPos - existing).LengthSquared() < dungeonDistSq)
                return false;
        }

        var activityDistSq = spawner.MinDistFromActivity * spawner.MinDistFromActivity;

        foreach (var pos in plotPositions)
        {
            if ((worldPos - pos).LengthSquared() < activityDistSq)
                return false;
        }

        foreach (var pos in shuttlePositions)
        {
            if ((worldPos - pos).LengthSquared() < activityDistSq)
                return false;
        }

        return true;
    }
}
