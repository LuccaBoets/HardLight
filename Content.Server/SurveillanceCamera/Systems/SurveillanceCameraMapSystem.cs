using System.Numerics;
using Content.Server.Power.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.SurveillanceCamera.Components;

namespace Content.Server.SurveillanceCamera;

public sealed class SurveillanceCameraMapSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SurveillanceCameraComponent, MoveEvent>(OnCameraMoved);
        SubscribeLocalEvent<SurveillanceCameraComponent, EntityUnpausedEvent>(OnCameraUnpaused);
        // HardLight: clean up the per-grid marker when the camera entity is removed so the
        // SurveillanceCameraMapComponent.Cameras dictionary doesn't accumulate dead entries
        // across the round (each entry is replicated to every observer of the grid via PVS,
        // so leaks here grow per-tick network payload as cameras are destroyed/recreated).
        // We listen on EntityTerminating rather than ComponentShutdown because
        // SurveillanceCameraSystem already owns the (SurveillanceCameraComponent, ComponentShutdown)
        // subscription slot and Robust's directed bus only allows one handler per (comp, event) pair.
        // EntityTerminating fires before the entity is removed, so the transform/grid lookup below
        // still resolves correctly.
        SubscribeLocalEvent<SurveillanceCameraComponent, EntityTerminatingEvent>(OnCameraTerminating);

        SubscribeNetworkEvent<RequestCameraMarkerUpdateMessage>(OnRequestCameraMarkerUpdate);
    }

    private void OnCameraUnpaused(EntityUid uid, SurveillanceCameraComponent comp, ref EntityUnpausedEvent args)
    {
        if (Terminating(uid))
            return;

        UpdateCameraMarker((uid, comp));
    }

    private void OnCameraMoved(EntityUid uid, SurveillanceCameraComponent comp, ref MoveEvent args)
    {
        if (Terminating(uid))
            return;

        // HardLight perf: cameras are anchored in normal play, so MoveEvent typically only
        // arrives on map-init or when a grid containing the camera moves and the transform
        // tree propagates. In the propagation case the parent (and therefore the grid) is
        // unchanged AND the local position is unchanged, so there is nothing to update and
        // we can skip the GetGrid lookups and the EnsureComp/Dirty path entirely.
        if (!args.ParentChanged && args.OldPosition.Position.Equals(args.NewPosition.Position))
            return;

        // Only do the cross-grid removal work when the parent actually changed -- if the
        // parent is the same entity, the resolved grid cannot have changed.
        if (args.ParentChanged)
        {
            var oldGridUid = _transform.GetGrid(args.OldPosition);
            var newGridUid = _transform.GetGrid(args.NewPosition);

            if (oldGridUid != newGridUid && oldGridUid is not null && !Terminating(oldGridUid.Value))
            {
                if (TryComp<SurveillanceCameraMapComponent>(oldGridUid, out var oldMapComp))
                {
                    var netEntity = GetNetEntity(uid);
                    if (oldMapComp.Cameras.Remove(netEntity))
                        Dirty(oldGridUid.Value, oldMapComp);
                }
            }

            if (newGridUid is not null && !Terminating(newGridUid.Value))
                UpdateCameraMarker((uid, comp));
            return;
        }

        // Same parent, but local position changed (rare for anchored cameras; e.g. an admin
        // teleport without re-anchoring). Refresh the marker so the position stays in sync.
        UpdateCameraMarker((uid, comp));
    }

    private void OnCameraTerminating(EntityUid uid, SurveillanceCameraComponent comp, ref EntityTerminatingEvent args)
    {
        // HardLight: walk the transform once to find the owning grid/map, then remove this
        // camera's NetEntity from the marker dictionary if present. Guarded so it works
        // whether the camera is being destroyed alone or as part of a grid teardown
        // (Terminating(grid) check avoids dirtying a grid that is itself shutting down).
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var gridUid = xform.GridUid ?? xform.MapUid;
        if (gridUid is null || Terminating(gridUid.Value))
            return;

        if (!TryComp<SurveillanceCameraMapComponent>(gridUid.Value, out var mapComp))
            return;

        var netEntity = GetNetEntity(uid);
        if (mapComp.Cameras.Remove(netEntity))
            Dirty(gridUid.Value, mapComp);
    }

    private void OnRequestCameraMarkerUpdate(RequestCameraMarkerUpdateMessage args)
    {
        var cameraEntity = GetEntity(args.CameraEntity);

        if (TryComp<SurveillanceCameraComponent>(cameraEntity, out var comp)
            && HasComp<DeviceNetworkComponent>(cameraEntity))
            UpdateCameraMarker((cameraEntity, comp));
    }

    /// <summary>
    /// Updates camera data in the SurveillanceCameraMapComponent for the specified camera entity.
    /// </summary>
    public void UpdateCameraMarker(Entity<SurveillanceCameraComponent> camera)
    {
        var (uid, comp) = camera;

        if (Terminating(uid))
            return;

        if (!TryComp(uid, out TransformComponent? xform) || !TryComp(uid, out DeviceNetworkComponent? deviceNet))
            return;

        var gridUid = xform.GridUid ?? xform.MapUid;
        if (gridUid is null)
            return;

        var netEntity = GetNetEntity(uid);

        var mapComp = EnsureComp<SurveillanceCameraMapComponent>(gridUid.Value);
        var worldPos = _transform.GetWorldPosition(xform);
        var gridMatrix = _transform.GetInvWorldMatrix(Transform(gridUid.Value));
        var localPos = Vector2.Transform(worldPos, gridMatrix);

        var address = deviceNet.Address;
        var subnet = deviceNet.ReceiveFrequencyId ?? string.Empty;
        var powered = CompOrNull<ApcPowerReceiverComponent>(uid)?.Powered ?? true;
        var active = comp.Active && powered;

        bool exists = mapComp.Cameras.TryGetValue(netEntity, out var existing);

        if (exists &&
            existing.Position.Equals(localPos) &&
            existing.Active == active &&
            existing.Address == address &&
            existing.Subnet == subnet)
        {
            return;
        }

        var visible = exists ? existing.Visible : true;

        mapComp.Cameras[netEntity] = new CameraMarker
        {
            Position = localPos,
            Active = active,
            Address = address,
            Subnet = subnet,
            Visible = visible
        };
        Dirty(gridUid.Value, mapComp);
    }

    /// <summary>
    /// Sets the visibility state of a camera on the camera map.
    /// </summary>
    public void SetCameraVisibility(EntityUid cameraUid, bool visible)
    {
        if (!TryComp(cameraUid, out TransformComponent? xform))
            return;

        var gridUid = xform.GridUid ?? xform.MapUid;
        if (gridUid == null || !TryComp<SurveillanceCameraMapComponent>(gridUid.Value, out var mapComp))
            return;

        var netEntity = GetNetEntity(cameraUid);
        if (mapComp.Cameras.TryGetValue(netEntity, out var marker))
        {
            marker.Visible = visible;
            mapComp.Cameras[netEntity] = marker;
            Dirty(gridUid.Value, mapComp);
        }
    }

    /// <summary>
    /// Checks if a camera is currently visible on the camera map.
    /// </summary>
    public bool IsCameraVisible(EntityUid cameraUid)
    {
        if (!TryComp(cameraUid, out TransformComponent? xform))
            return false;

        var gridUid = xform.GridUid ?? xform.MapUid;
        if (gridUid == null || !TryComp<SurveillanceCameraMapComponent>(gridUid, out var mapComp))
            return false;

        var netEntity = GetNetEntity(cameraUid);
        return mapComp.Cameras.TryGetValue(netEntity, out var marker) && marker.Visible;
    }
}
