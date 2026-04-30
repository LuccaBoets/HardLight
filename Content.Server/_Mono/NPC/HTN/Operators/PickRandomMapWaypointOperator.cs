using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Mono.NPC.HTN.Operators;

/// <summary>
/// Picks a random map waypoint inside an annulus around world origin and
/// writes it as <see cref="EntityCoordinates"/> to <see cref="OutputKey"/>
/// on the blackboard. Used by <see cref="ShipMoveToOperator"/> to drive
/// "smart" patrols without needing a target entity.
/// </summary>
public sealed partial class PickRandomMapWaypointOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Blackboard key the picked <see cref="EntityCoordinates"/> are written to.
    /// </summary>
    [DataField]
    public string OutputKey = "ShipPatrolWaypoint";

    /// <summary>
    /// Inner radius of the annulus, in metres from world origin.
    /// </summary>
    [DataField]
    public float MinDistance = 7000f;

    /// <summary>
    /// Outer radius of the annulus, in metres from world origin.
    /// </summary>
    [DataField]
    public float MaxDistance = 20000f;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, _entManager))
            return (false, null);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform))
            return (false, null);

        var mapUid = xform.MapUid;
        if (mapUid == null || !mapUid.Value.IsValid())
            return (false, null);

        // Uniform sample in an annulus: angle uniform, radius weighted by sqrt
        // so points are area-uniform rather than clustered toward the inner ring.
        var angle = _random.NextFloat(0f, MathF.Tau);
        var minSq = MinDistance * MinDistance;
        var maxSq = MaxDistance * MaxDistance;
        var radius = MathF.Sqrt(_random.NextFloat(minSq, maxSq));
        var pos = new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);

        var coords = new EntityCoordinates(mapUid.Value, pos);

        return (true, new Dictionary<string, object>
        {
            { OutputKey, coords },
        });
    }
}
