using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Maths;

namespace Robust.Shared.Map.Components;

/// <summary>
/// Temporary compatibility shims for legacy MapGridComponent instance APIs removed upstream.
/// </summary>
public static class LegacyMapGridComponentExtensions
{
    public static Vector2i TileIndicesFor(this MapGridComponent grid, EntityCoordinates coords)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.TileIndicesFor(grid.Owner, grid, coords);
    }

    public static EntityCoordinates GridTileToLocal(this MapGridComponent grid, Vector2i tile)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.GridTileToLocal(grid.Owner, grid, tile);
    }

    public static Vector2 WorldToLocal(this MapGridComponent grid, Vector2 worldPos)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.WorldToLocal(grid.Owner, grid, worldPos);
    }

    public static IEnumerable<EntityUid> GetAnchoredEntities(this MapGridComponent grid, Vector2i pos)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.GetAnchoredEntities(grid.Owner, grid, pos);
    }

    public static IEnumerable<EntityUid> GetAnchoredEntities(this MapGridComponent grid, MapCoordinates coords)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.GetAnchoredEntities(grid.Owner, grid, coords);
    }

    public static AnchoredEntitiesEnumerator GetAnchoredEntitiesEnumerator(this MapGridComponent grid, Vector2i pos)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.GetAnchoredEntitiesEnumerator(grid.Owner, grid, pos);
    }

    public static bool TryGetTileRef(this MapGridComponent grid, Vector2i indices, out TileRef tile)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.TryGetTileRef(grid.Owner, grid, indices, out tile);
    }

    public static bool TryGetTileRef(this MapGridComponent grid, EntityCoordinates coords, out TileRef tile)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.TryGetTileRef(grid.Owner, grid, coords, out tile);
    }

    public static IEnumerable<TileRef> GetLocalTilesIntersecting(this MapGridComponent grid, Circle localCircle, bool ignoreEmpty = true)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.GetLocalTilesIntersecting(grid.Owner, grid, localCircle, ignoreEmpty);
    }

    public static IEnumerable<TileRef> GetLocalTilesIntersecting(this MapGridComponent grid, Box2 localAabb, bool ignoreEmpty = true)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.GetLocalTilesIntersecting(grid.Owner, grid, localAabb, ignoreEmpty);
    }

    public static IEnumerable<TileRef> GetLocalTilesIntersecting(this MapGridComponent grid, Box2Rotated localArea, bool ignoreEmpty = true)
    {
        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return mapSys.GetLocalTilesIntersecting(grid.Owner, grid, localArea, ignoreEmpty);
    }
}
