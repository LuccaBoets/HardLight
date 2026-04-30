using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Robust.Shared.Random;

namespace Content.Server._Mono.NPC.HTN.Preconditions;

/// <summary>
/// HTN precondition that succeeds with the given probability.
/// Evaluated only during planning (not per-tick), so cost is negligible:
/// a single <see cref="IRobustRandom.Prob"/> call per branch consideration.
/// Useful for adding non-deterministic branch selection (e.g. a chance to
/// break off and reposition between attack runs) without writing a new
/// operator or polluting the blackboard.
/// </summary>
public sealed partial class RandomChancePrecondition : HTNPrecondition
{
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Probability in [0, 1] that this precondition is met on a given
    /// planning attempt.
    /// </summary>
    [DataField]
    public float Chance = 0.5f;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        return _random.Prob(Chance);
    }
}
