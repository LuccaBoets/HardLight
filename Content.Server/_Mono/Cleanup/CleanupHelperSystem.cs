using System.Numerics;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server._Mono.Cleanup;

/// <summary>
/// Utility helpers for cleanup systems to check whether players or grids are near a coordinate.
/// </summary>
public sealed class CleanupHelperSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;

    // Reused per call to avoid allocating a fresh list every cleanup tick.
    // Not readonly because FindGridsIntersecting takes the list by ref.
    private List<Entity<MapGridComponent>> _scratchGrids = new();

    public bool HasNearbyPlayers(EntityCoordinates coordinates, float maxDistance)
    {
        if (maxDistance <= 0f)
            return false;

        var mapCoords = _xform.ToMapCoordinates(coordinates);
        var mapId = mapCoords.MapId;
        if (mapId == MapId.Nullspace)
            return false;

        var position = mapCoords.Position;
        var maxDistanceSq = maxDistance * maxDistance;

        foreach (var session in _players.Sessions)
        {
            var player = session.AttachedEntity;
            if (player is not { Valid: true })
                continue;

            if (!TryComp<TransformComponent>(player.Value, out var xform))
                continue;

            if (xform.MapID != mapId)
                continue;

            var playerPos = _xform.GetWorldPosition(xform);
            if (Vector2.DistanceSquared(position, playerPos) <= maxDistanceSq)
                return true;
        }

        return false;
    }

    public bool HasNearbyGrids(EntityCoordinates coordinates, float maxDistance)
    {
        if (maxDistance <= 0f)
            return false;

        var mapCoords = _xform.ToMapCoordinates(coordinates);
        var mapId = mapCoords.MapId;
        if (mapId == MapId.Nullspace)
            return false;

        var position = mapCoords.Position;
        var maxDistanceSq = maxDistance * maxDistance;

        // Use the broadphase to fetch only grids whose AABB overlaps the search box,
        // then preserve original semantics ("any grid origin within maxDistance") by
        // distance-checking each candidate. Avoids walking every MapGridComponent.
        var box = Box2.CenteredAround(position, new Vector2(maxDistance * 2f, maxDistance * 2f));
        _scratchGrids.Clear();
        _mapMan.FindGridsIntersecting(mapId, box, ref _scratchGrids, approx: true);

        foreach (var grid in _scratchGrids)
        {
            if (!TryComp<TransformComponent>(grid.Owner, out var gridXform))
                continue;

            var gridPos = _xform.GetWorldPosition(gridXform);
            if (Vector2.DistanceSquared(position, gridPos) <= maxDistanceSq)
            {
                _scratchGrids.Clear();
                return true;
            }
        }

        _scratchGrids.Clear();
        return false;
    }
}
