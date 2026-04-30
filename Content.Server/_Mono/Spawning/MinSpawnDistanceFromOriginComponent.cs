namespace Content.Server._Mono.Spawning;

/// <summary>
/// Marks a <see cref="GridSpawnerComponent"/> as only allowed to spawn
/// when its world position is at least <see cref="Distance"/> metres from
/// the map origin (0,0). Used to keep hostile worldgen drone spawns out
/// of the inner trade hub area.
/// </summary>
[RegisterComponent]
public sealed partial class MinSpawnDistanceFromOriginComponent : Component
{
    /// <summary>
    /// Minimum allowed distance from world origin (0,0) in metres.
    /// Spawners closer than this are silently deleted at MapInit.
    /// </summary>
    [DataField]
    public float Distance = 5000f;
}
