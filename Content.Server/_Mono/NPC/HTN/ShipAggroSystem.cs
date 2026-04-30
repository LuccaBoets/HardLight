using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Server.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Drives <see cref="ShipAggroComponent"/>: refreshes aggro on incoming
/// ship-weapon hits (notified by <c>SpaceArtillerySystem</c>) and on
/// hostile <see cref="ShipNpcTargetComponent"/> entities entering
/// proximity. Mirrors aggro state into the HTN blackboard so HTN
/// compounds can branch into chase behavior.
/// </summary>
public sealed class ShipAggroSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    // Throttle proximity scans (cheap query but no need to do it every tick).
    private static readonly TimeSpan ProximityScanInterval = TimeSpan.FromSeconds(1);
    // Stations almost never move; cache their positions aggressively and
    // refresh infrequently.
    private static readonly TimeSpan StationCacheRefreshInterval = TimeSpan.FromSeconds(60);
    private TimeSpan _nextProximityScan = TimeSpan.Zero;
    private TimeSpan _nextStationCacheRefresh = TimeSpan.Zero;
    private readonly Dictionary<MapId, List<(EntityUid MapUid, Vector2 Position)>> _stationPositionsByMap = new();

    /// <summary>
    /// Aggro any AI cores on the grid that just took ship-weapon fire.
    /// Called from <c>SpaceArtillerySystem.OnProjectileHit</c>.
    /// </summary>
    public void NotifyGridHit(EntityUid grid)
    {
        AggroCoresOnGrid(grid);
    }

    private void AggroCoresOnGrid(EntityUid grid)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ShipAggroComponent, TransformComponent>();
        while (query.MoveNext(out _, out var aggro, out var coreXform))
        {
            if (coreXform.GridUid != grid)
                continue;

            aggro.AggroEndTime = now + aggro.AggroDuration;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        if (now >= _nextProximityScan)
        {
            _nextProximityScan = now + ProximityScanInterval;
            ScanProximity(now);
            ScanStationAvoidance(now);
        }

        // Sync blackboard state on every aggro core (cheap; small population).
        var sync = EntityQueryEnumerator<ShipAggroComponent, HTNComponent>();
        while (sync.MoveNext(out _, out var aggro, out var htn))
        {
            var aggroed = now < aggro.AggroEndTime;
            var hasKey = htn.Blackboard.ContainsKey(aggro.BlackboardKey);
            if (aggroed && !hasKey)
                htn.Blackboard.SetValue(aggro.BlackboardKey, true);
            else if (!aggroed && hasKey)
                htn.Blackboard.Remove<bool>(aggro.BlackboardKey);

            // Mirror station-avoidance flee waypoint onto blackboard.
            var hasAvoid = htn.Blackboard.ContainsKey(aggro.AvoidStationBlackboardKey);
            if (aggro.PendingAvoidStationCoordinates is { } flee)
            {
                htn.Blackboard.SetValue(aggro.AvoidStationBlackboardKey, flee);
            }
            else if (hasAvoid)
            {
                htn.Blackboard.Remove<EntityCoordinates>(aggro.AvoidStationBlackboardKey);
            }
        }
    }

    private void ScanProximity(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ShipAggroComponent, TransformComponent>();
        while (query.MoveNext(out var coreUid, out var aggro, out var coreXform))
        {
            var coreGrid = coreXform.GridUid;
            if (coreGrid == null)
                continue;

            var corePos = _xform.GetMapCoordinates(coreUid, coreXform);
            var aggroed = now < aggro.AggroEndTime;

            // Use the larger leash range for the lookup; a hostile inside
            // AggroProximityRange is the only thing that can *start* aggro,
            // but anything inside AggroLeashRange will *maintain* aggro
            // once started (so it only fades after the target is past the
            // leash AND AggroDuration has elapsed).
            var scanRange = MathF.Max(aggro.AggroProximityRange, aggro.AggroLeashRange);
            foreach (var found in _lookup.GetEntitiesInRange<ShipNpcTargetComponent>(corePos, scanRange))
            {
                var targetXform = Transform(found.Owner);
                var targetGrid = targetXform.GridUid;
                if (targetGrid == null || targetGrid == coreGrid)
                    continue;

                var targetPos = _xform.GetMapCoordinates(found.Owner, targetXform);
                if (targetPos.MapId != corePos.MapId)
                    continue;

                var distSq = (targetPos.Position - corePos.Position).LengthSquared();

                if (aggroed)
                {
                    if (distSq <= aggro.AggroLeashRange * aggro.AggroLeashRange)
                    {
                        aggro.AggroEndTime = now + aggro.AggroDuration;
                        break;
                    }
                }
                else
                {
                    if (distSq <= aggro.AggroProximityRange * aggro.AggroProximityRange)
                    {
                        aggro.AggroEndTime = now + aggro.AggroDuration;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Pushes AI cores away from player stations: any aggro core whose
    /// grid is closer than <see cref="ShipAggroComponent.AvoidStationRange"/>
    /// to a station grid has its aggro force-cleared and gets a flee
    /// waypoint set on its blackboard, pointing directly away from the
    /// nearest station to a point just outside the avoidance border.
    /// </summary>
    private void ScanStationAvoidance(TimeSpan now)
    {
        var cacheReady = false;

        var query = EntityQueryEnumerator<ShipAggroComponent, TransformComponent>();
        while (query.MoveNext(out var coreUid, out var aggro, out var coreXform))
        {
            if (aggro.AvoidStationRange <= 0f)
            {
                aggro.PendingAvoidStationCoordinates = null;
                continue;
            }

            var coreMap = coreXform.MapID;
            if (coreMap == MapId.Nullspace)
            {
                aggro.PendingAvoidStationCoordinates = null;
                continue;
            }

            // Lazily refresh only when we actually need station positions.
            if (!cacheReady)
            {
                if (_stationPositionsByMap.Count == 0 || now >= _nextStationCacheRefresh)
                {
                    _nextStationCacheRefresh = now + StationCacheRefreshInterval;
                    RebuildStationPositionCache();
                }

                cacheReady = true;
            }

            if (!_stationPositionsByMap.TryGetValue(coreMap, out var stationPositions) || stationPositions.Count == 0)
            {
                aggro.PendingAvoidStationCoordinates = null;
                continue;
            }

            var corePos = _xform.GetMapCoordinates(coreUid, coreXform).Position;
            var avoidRangeSq = aggro.AvoidStationRange * aggro.AvoidStationRange;

            // Find the nearest station grid on the same map.
            var bestDistSq = float.MaxValue;
            var bestStationPos = Vector2.Zero;
            EntityUid? bestMapUid = null;

            foreach (var (mapUid, pos) in stationPositions)
            {
                var d = (pos - corePos).LengthSquared();
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    bestStationPos = pos;
                    bestMapUid = mapUid;
                }
            }

            if (bestMapUid == null || bestDistSq >= avoidRangeSq)
            {
                aggro.PendingAvoidStationCoordinates = null;
                continue;
            }

            // Inside the no-go zone: drop aggro and aim outward.
            aggro.AggroEndTime = TimeSpan.Zero;

            var awayDir = corePos - bestStationPos;
            var awayLen = awayDir.Length();
            // Degenerate: AI sitting on top of station; pick a stable arbitrary heading.
            var unit = awayLen > 0.001f
                ? awayDir / awayLen
                : new Vector2(1f, 0f);

            var fleeOffset = aggro.AvoidStationRange + aggro.AvoidStationBuffer;
            var fleePos = bestStationPos + unit * fleeOffset;

            aggro.PendingAvoidStationCoordinates = new EntityCoordinates(bestMapUid.Value, fleePos);
        }
    }

    private void RebuildStationPositionCache()
    {
        _stationPositionsByMap.Clear();

        var stations = EntityQueryEnumerator<StationDataComponent>();
        while (stations.MoveNext(out _, out var station))
        {
            foreach (var grid in station.Grids)
            {
                if (!TryComp<TransformComponent>(grid, out var gridXform))
                    continue;

                var stationMapId = gridXform.MapID;
                var stationMapUid = gridXform.MapUid;
                if (stationMapId == MapId.Nullspace || stationMapUid == null)
                    continue;

                var gridPos = _xform.GetMapCoordinates(grid, gridXform).Position;
                if (!_stationPositionsByMap.TryGetValue(stationMapId, out var positions))
                {
                    positions = new List<(EntityUid MapUid, Vector2 Position)>();
                    _stationPositionsByMap[stationMapId] = positions;
                }

                positions.Add((stationMapUid.Value, gridPos));
            }
        }
    }
}
