using System.Numerics;
using Content.Server._Hardlight.StationEvents.Events;
using Content.Server._NF.Station.Components; // HardLight
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components; // HardLight
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed class MeteorSwarmSystem : GameRuleSystem<MeteorSwarmComponent>
{
    private const int SpawnClearanceAttempts = 6; // HardLight
    private const float SpawnClearanceMargin = 25f; // HardLight

    [Dependency] private readonly IMapManager _mapManager = default!; // HardLight
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StationSystem _station = default!;

    protected override void Added(EntityUid uid, MeteorSwarmComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        component.WaveCounter = component.Waves.Next(RobustRandom);
        component.NextWaveTime = Timing.CurTime + TimeSpan.FromSeconds(component.WaveCooldown.Next(RobustRandom)); // HardLight

        // we don't want to send to players who aren't in game (i.e. in the lobby)
        Filter allPlayersInGame = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);

        if (component.Announcement is { } locId)
            _chat.DispatchFilteredAnnouncement(allPlayersInGame, Loc.GetString(locId), playSound: false, colorOverride: Color.Gold);

        _audio.PlayGlobal(component.AnnouncementSound, allPlayersInGame, true);
    }

    protected override void ActiveTick(EntityUid uid, MeteorSwarmComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (Timing.CurTime < component.NextWaveTime)
            return;

        component.NextWaveTime += TimeSpan.FromSeconds(component.WaveCooldown.Next(RobustRandom));

        // HardLight: Only target actual station body grids, not attached/purchased shuttles.
        var candidates = new List<(EntityUid Station, EntityUid Grid)>();
        foreach (var station in _station.GetStations())
        {
            if (!station.Valid ||
                !HasComp<ValidMeteorSwarmComponent>(station) ||
                HasComp<ExtraShuttleInformationComponent>(station))
                continue;

            if (!TryComp<StationDataComponent>(station, out var stationData))
                continue;

            if (TryGetMeteorTargetGrid(stationData) is not { } targetGrid)
                continue;

            candidates.Add((station, targetGrid));
        }

        if (candidates.Count == 0) // HardLight
            return;

        var (_, grid) = RobustRandom.Pick(candidates); // HardLight

        var mapId = Transform(grid).MapID;
        var playableArea = _physics.GetWorldAABB(grid);

        var minimumDistance = (playableArea.TopRight - playableArea.Center).Length() + 50f;
        var maximumDistance = minimumDistance + 100f;

        var center = playableArea.Center;

        var meteorsToSpawn = component.MeteorsPerWave.Next(RobustRandom);
        for (var i = 0; i < meteorsToSpawn; i++)
        {
            var spawnProto = RobustRandom.Pick(component.Meteors);

            var angle = component.NonDirectional
                ? RobustRandom.NextAngle()
                : new Random(uid.Id).NextAngle();

            var offset = angle.RotateVec(new Vector2((maximumDistance - minimumDistance) * RobustRandom.NextFloat() + minimumDistance, 0));

            // the line at which spawns occur is perpendicular to the offset.
            // This means the meteors are less likely to bunch up and hit the same thing.
            var subOffsetAngle = RobustRandom.Prob(0.5f)
                ? angle + Math.PI / 2
                : angle - Math.PI / 2;
            var subOffset = subOffsetAngle.RotateVec(new Vector2( (playableArea.TopRight - playableArea.Center).Length() / 3 * RobustRandom.NextFloat(), 0));

            var spawnPosition = FindSpawnPosition(center, mapId, offset, subOffset); // HardLight
            var meteor = Spawn(spawnProto, spawnPosition);
            var physics = Comp<PhysicsComponent>(meteor);
            _physics.ApplyLinearImpulse(meteor, -offset.Normalized() * component.MeteorVelocity * physics.Mass, body: physics);
        }

        component.WaveCounter--;
        if (component.WaveCounter <= 0)
        {
            ForceEndSelf(uid, gameRule);
        }
    }

    private EntityUid? TryGetMeteorTargetGrid(StationDataComponent stationData) // HardLight
    {
        EntityUid? largestGrid = null;
        var largestSize = 0f;

        foreach (var gridUid in stationData.Grids)
        {
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
            {
                continue;
            }

            var size = grid.LocalAABB.Size.LengthSquared();
            if (size <= largestSize)
                continue;

            largestSize = size;
            largestGrid = gridUid;
        }

        return largestGrid;
    }

    private MapCoordinates FindSpawnPosition(Vector2 center, MapId mapId, Vector2 offset, Vector2 subOffset) // HardLight
    {
        var outwardDirection = offset.Normalized();
        var spawnPosition = center + offset + subOffset;

        for (var attempt = 0; attempt < SpawnClearanceAttempts; attempt++)
        {
            if (!_mapManager.TryFindGridAt(mapId, spawnPosition, out var blockingGrid, out _))
                return new MapCoordinates(spawnPosition, mapId);

            spawnPosition += outwardDirection * GetPushOutDistance(blockingGrid, spawnPosition, outwardDirection);
        }

        return new MapCoordinates(spawnPosition, mapId);
    }

    private float GetPushOutDistance(EntityUid blockingGrid, Vector2 spawnPosition, Vector2 outwardDirection) // HardLight
    {
        var blockingBounds = _physics.GetWorldAABB(blockingGrid);
        var furthestProjection = GetFurthestProjection(blockingBounds, outwardDirection);
        var currentProjection = Vector2.Dot(spawnPosition, outwardDirection);

        return MathF.Max(furthestProjection - currentProjection, 0f) + SpawnClearanceMargin;
    }

    private static float GetFurthestProjection(Box2 bounds, Vector2 direction) // HardLight
    {
        var furthest = Vector2.Dot(bounds.BottomLeft, direction);
        furthest = MathF.Max(furthest, Vector2.Dot(bounds.BottomRight, direction));
        furthest = MathF.Max(furthest, Vector2.Dot(bounds.TopLeft, direction));
        furthest = MathF.Max(furthest, Vector2.Dot(bounds.TopRight, direction));
        return furthest;
    }
}
