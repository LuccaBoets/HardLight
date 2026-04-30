using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Preferences;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed class ContainerSpawnPointSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawningEvent>(HandlePlayerSpawning, before: new []{ typeof(SpawnPointSystem) });
    }

    public void HandlePlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        // DeltaV - Ignore these two desired spawn types
        if (args.DesiredSpawnPointType is SpawnPointType.Observer or SpawnPointType.LateJoin)
            return;

        // If it's just a spawn pref check if it's for cryo (silly).
        if (args.HumanoidCharacterProfile?.SpawnPriority != SpawnPriorityPreference.Cryosleep &&
            (!_proto.TryIndex(args.Job, out var jobProto) || jobProto.JobEntity == null))
        {
            return;
        }

        var query = EntityQueryEnumerator<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>();
        var possibleContainers = new List<Entity<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>>();

        while (query.MoveNext(out var uid, out var spawnPoint, out var container, out var xform))
        {
            if (args.Station != null && _station.GetOwningStation(uid, xform) != args.Station)
                continue;

            // If it's unset, then we allow it to be used for both roundstart and midround joins
            if (spawnPoint.SpawnType == SpawnPointType.Unset)
            {
                // make sure we also check the job here for various reasons.
                if (spawnPoint.Job == null || spawnPoint.Job == args.Job)
                    possibleContainers.Add((uid, spawnPoint, container, xform));
                continue;
            }

            if (_gameTicker.RunLevel == GameRunLevel.InRound && spawnPoint.SpawnType == SpawnPointType.LateJoin)
            {
                possibleContainers.Add((uid, spawnPoint, container, xform));
            }

            if (_gameTicker.RunLevel != GameRunLevel.InRound &&
                spawnPoint.SpawnType == SpawnPointType.Job &&
                (args.Job == null || spawnPoint.Job == args.Job))
            {
                possibleContainers.Add((uid, spawnPoint, container, xform));
            }
        }

        if (possibleContainers.Count == 0)
            return;

        // HardLight start
        _random.Shuffle(possibleContainers);

        Entity<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>? selectedContainer = null;
        BaseContainer? targetContainer = null;
        foreach (var containerCandidate in possibleContainers)
        {
            if (!_container.TryGetContainer(containerCandidate.Owner, containerCandidate.Comp1.ContainerId, out var resolvedContainer, containerCandidate.Comp2))
                continue;

            // Avoid spawning and charging paid loadouts unless a cryo slot is actually available.
            if (resolvedContainer.ContainedEntities.Count != 0)
                continue;

            selectedContainer = containerCandidate;
            targetContainer = resolvedContainer;
            break;
        }

        if (selectedContainer == null || targetContainer == null)
            return;

        var baseCoords = selectedContainer.Value.Comp3.Coordinates;
        // HardLight end

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            baseCoords,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station,
            session: args.Session); // Frontier

        if (_container.Insert(args.SpawnResult.Value, targetContainer, containerXform: selectedContainer.Value.Comp3)) // HardLight
        {
            var ev = new ContainerSpawnEvent(args.SpawnResult.Value);
            RaiseLocalEvent(selectedContainer.Value.Owner, ref ev); // HardLight: uid<selectedContainer.Value.Owner
            return;
        }

        Del(args.SpawnResult);
        args.SpawnResult = null;
    }
}

/// <summary>
/// Raised on a container when a player is spawned into it.
/// </summary>
[ByRefEvent]
public record struct ContainerSpawnEvent(EntityUid Player);
