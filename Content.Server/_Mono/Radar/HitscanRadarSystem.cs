using Content.Server._Mono.FireControl;
using Content.Shared._Mono.Radar;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared.Weapons.Ranged;
using System.Numerics;

namespace Content.Server._Mono.Radar;

/// <summary>
/// Tracks transient hitscan radar signatures and shares them with clients.
/// </summary>
public sealed partial class HitscanRadarSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<HitscanNetData, TimeSpan> _activeHitscans = new();

    /// <summary>
    /// Event raised before firing the effects for a hitscan projectile.
    /// </summary>
    public sealed class HitscanFireEffectEvent : EntityEventArgs
    {
        public EntityCoordinates FromCoordinates { get; }
        public float Distance { get; }
        public Angle Angle { get; }
        public HitscanPrototype Hitscan { get; }
        public EntityUid? HitEntity { get; }
        public EntityUid? Gun { get; }
        public EntityUid? Shooter { get; }

        public HitscanFireEffectEvent(EntityCoordinates fromCoordinates, float distance, Angle angle, HitscanPrototype hitscan, EntityUid? hitEntity = null, EntityUid? gun = null, EntityUid? shooter = null)
        {
            FromCoordinates = fromCoordinates;
            Distance = distance;
            Angle = angle;
            Hitscan = hitscan;
            HitEntity = hitEntity;
            Gun = gun;
            Shooter = shooter;
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HitscanFireEffectEvent>(OnHitscanEffect);
        SubscribeLocalEvent<HitscanRadarSignatureComponent, HitscanRaycastFiredEvent>(OnSignatureHitscanFired);
    }

    private void OnHitscanEffect(HitscanFireEffectEvent ev)
    {
        if (ev.Gun == null || !HasComp<FireControllableComponent>(ev.Gun.Value))
            return;

        var startPos = _transform.ToMapCoordinates(ev.FromCoordinates).Position;
        var dir = ev.Angle.ToVec().Normalized();
        var endPos = startPos + dir * ev.Distance;

        // HardLight: defaults sized so player ship lasers (which lack a
        // HitscanRadar/Signature config block) actually render on the
        // gunnery console radar instead of being a 1-pixel magenta line
        // visible for a single client poll. Cyan + 2px + 1.0s matches the
        // visibility of kinetic projectile RadarBlips.
        var color = Color.Cyan;
        var thickness = 2.0f;
        var lifeTime = 1.0f;

        if (TryComp<HitscanRadarComponent>(ev.Gun.Value, out var shooterHitscanRadar))
        {
            color = shooterHitscanRadar.RadarColor;
            thickness = shooterHitscanRadar.LineThickness;
            lifeTime = shooterHitscanRadar.LifeTime;
        }
        else if (TryComp<HitscanRadarSignatureComponent>(ev.Gun.Value, out var signature))
        {
            color = signature.RadarColor ?? Color.Red;
            thickness = 2f;
            lifeTime = signature.LifeTime > 0f ? signature.LifeTime : 0.5f;
        }

        if (!TryCreateHitscanData(startPos, endPos, Transform(ev.Gun.Value).GridUid, thickness, color, out var hitscan))
            return;

        QueueHitscan(hitscan, TimeSpan.FromSeconds(lifeTime));
    }

    private void OnSignatureHitscanFired(EntityUid uid, HitscanRadarSignatureComponent component, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled || args.Gun == null)
            return;

        if (HasComp<FireControllableComponent>(args.Gun.Value))
            return;

        if (!TryComp<TransformComponent>(args.Gun.Value, out var gunXform) || !gunXform.MapUid.HasValue)
            return;

        var startPos = _transform.GetMapCoordinates(args.Gun.Value).Position;

        Vector2 endPos;
        if (args.HitEntity != null)
        {
            endPos = _transform.GetMapCoordinates(args.HitEntity.Value).Position;
        }
        else
        {
            var worldRot = _transform.GetWorldRotation(args.Gun.Value);
            var direction = worldRot.ToWorldVec();
            const float maxLength = 45f;
            endPos = startPos + direction * maxLength;
        }

        var color = component.RadarColor ?? Color.Red;
        var lifeTime = component.LifeTime > 0f ? component.LifeTime : 0.5f;

        if (!TryCreateHitscanData(startPos, endPos, gunXform.GridUid, 2f, color, out var hitscan))
            return;

        QueueHitscan(hitscan, TimeSpan.FromSeconds(lifeTime));
    }

    public void CopyVisibleHitscans(List<Vector2> sourcePositions, float radarRange, List<HitscanNetData> destination)
    {
        var radarRangeSq = radarRange * radarRange;

        foreach (var hitscan in _activeHitscans.Keys)
        {
            if (IsHitscanVisible(hitscan, sourcePositions, radarRangeSq))
                destination.Add(hitscan);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_activeHitscans.Count == 0)
            return;

        var currentTime = _timing.CurTime;
        List<HitscanNetData>? expiredHitscans = null;

        foreach (var (hitscan, deleteTime) in _activeHitscans)
        {
            if (currentTime < deleteTime)
                continue;

            expiredHitscans ??= new List<HitscanNetData>();
            expiredHitscans.Add(hitscan);
        }

        if (expiredHitscans == null)
            return;

        foreach (var hitscan in expiredHitscans)
        {
            _activeHitscans.Remove(hitscan);
        }
    }

    private void QueueHitscan(HitscanNetData hitscan, TimeSpan lifeTime)
    {
        _activeHitscans[hitscan] = _timing.CurTime + lifeTime;
    }

    private bool IsHitscanVisible(HitscanNetData hitscan, List<Vector2> sourcePositions, float radarRangeSq)
    {
        if (hitscan.Grid == null)
        {
            return IsSegmentNearAnySource(hitscan.Start, hitscan.End, sourcePositions, radarRangeSq);
        }

        if (!TryGetEntity(hitscan.Grid, out var gridEntity))
            return false;

        var worldPos = _transform.GetWorldPosition(gridEntity.Value);
        var gridRot = _transform.GetWorldRotation(gridEntity.Value);
        var worldStart = worldPos + gridRot.RotateVec(hitscan.Start);
        var worldEnd = worldPos + gridRot.RotateVec(hitscan.End);

        return IsSegmentNearAnySource(worldStart, worldEnd, sourcePositions, radarRangeSq);
    }

    // Mono: check the full line segment, not just endpoints, so crossing beams stay visible.
    private static bool IsSegmentNearAnySource(Vector2 start, Vector2 end, List<Vector2> sourcePositions, float radarRangeSq)
    {
        foreach (var sourcePosition in sourcePositions)
        {
            if (DistanceSquaredToSegment(sourcePosition, start, end) <= radarRangeSq)
                return true;
        }

        return false;
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();

        if (lengthSquared <= float.Epsilon)
            return Vector2.DistanceSquared(point, start);

        var t = Vector2.Dot(point - start, segment) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);
        var closestPoint = start + segment * t;
        return Vector2.DistanceSquared(point, closestPoint);
    }

    private bool TryCreateHitscanData(Vector2 startPos, Vector2 endPos, EntityUid? originGrid, float thickness, Color color, out HitscanNetData hitscan)
    {
        if (originGrid != null && originGrid.Value.IsValid())
        {
            var gridMatrix = _transform.GetWorldMatrix(originGrid.Value);
            Matrix3x2.Invert(gridMatrix, out var invGridMatrix);

            var localStart = Vector2.Transform(startPos, invGridMatrix);
            var localEnd = Vector2.Transform(endPos, invGridMatrix);
            hitscan = new HitscanNetData(GetNetEntity(originGrid.Value), localStart, localEnd, thickness, color);
            return true;
        }

        hitscan = new HitscanNetData(null, startPos, endPos, thickness, color);
        return true;
    }
}
