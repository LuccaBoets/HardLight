using Content.Shared.Shuttles.Components;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Shared helper for objective systems that need to answer
/// "does this entity count as having made it to safety at round end?"
/// without caring about which transport path got them there.
/// </summary>
public sealed class RoundEndArrivalSystem : EntitySystem
{
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// Returns true if the entity is on ColComm or on an active expedition map.
    /// This intentionally ignores whether they used evac, a private shuttle, or a portal.
    /// </summary>
    public bool CountsAsArrived(EntityUid entity)
    {
        var xform = Transform(entity);
        var mapUid = xform.MapUid;
        if (mapUid == null)
            return false;

        if (_emergencyShuttle.IsColcommMap(mapUid.Value))
            return true;

        if (HasComp<SalvageExpeditionComponent>(mapUid.Value))
            return true;

        // End-round owned/transit shuttles are queued to FTL to ColComm before
        // objective resolution runs. Count passengers on those grids as arrived
        // even if the map transition has not completed yet.
        if (xform.GridUid != null &&
            TryComp<FTLComponent>(xform.GridUid.Value, out var ftl) &&
            _emergencyShuttle.IsColcommMap(GetTargetMap(ftl.TargetCoordinates)))
        {
            return true;
        }

        return false;
    }

    private EntityUid GetTargetMap(EntityCoordinates coordinates)
    {
        var mapId = _transform.GetMapId(coordinates);
        return _transform.GetMap(coordinates) ?? _mapManager.GetMapEntityId(mapId);
    }
}
