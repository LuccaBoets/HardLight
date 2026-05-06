using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using RobustTimer = Robust.Shared.Timing.Timer;

namespace Robust.Shared.GameObjects
{

/// <summary>
/// Temporary compatibility shims for APIs removed upstream.
/// </summary>
public static class LegacyEntityCompatibilityExtensions
{
    public static void SpawnTimer(this EntityUid uid, int milliseconds, Action onFired, CancellationToken cancellationToken = default)
    {
        RobustTimer.Spawn(milliseconds, onFired, cancellationToken);
    }

    public static void SpawnTimer(this EntityUid uid, TimeSpan duration, Action onFired, CancellationToken cancellationToken = default)
    {
        RobustTimer.Spawn(duration, onFired, cancellationToken);
    }

    public static void SpawnRepeatingTimer(this EntityUid uid, int milliseconds, Action onFired, CancellationToken cancellationToken)
    {
        RobustTimer.SpawnRepeating(milliseconds, onFired, cancellationToken);
    }

    public static void SpawnRepeatingTimer(this EntityUid uid, TimeSpan duration, Action onFired, CancellationToken cancellationToken)
    {
        RobustTimer.SpawnRepeating(duration, onFired, cancellationToken);
    }

    public static Tile Tile(this IEntityManager entMan, TileRef tileRef)
    {
        return tileRef.Tile;
    }
}

}

namespace Robust.Shared.Containers
{

public static class LegacyContainerCompatibilityExtensions
{
    public static bool TryGetContainer(
        this ContainerManagerComponent containerManager,
        string id,
        [NotNullWhen(true)] out BaseContainer? container)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var containerSys = entMan.System<SharedContainerSystem>();
        return containerSys.TryGetContainer(containerManager.Owner, id, out container, containerManager);
    }
}

}
