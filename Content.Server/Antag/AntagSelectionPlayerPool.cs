using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Antag;

public sealed class AntagSelectionPlayerPool (List<List<ICommonSession>> orderedPools)
{
    public bool TryPickAndTake(IRobustRandom random, [NotNullWhen(true)] out ICommonSession? session)
    {
        session = null;

        foreach (var pool in orderedPools)
        {
            if (pool.Count == 0)
                continue;

            session = random.PickAndTake(pool);
            break;
        }

        return session != null;
    }

    /// <summary>
    /// VRS: pick the next antag candidate using <see cref="AntagOptOutSystem"/>'s anti-repeat
    /// weighting. Weighting is applied within each tier (so preferred candidates are still
    /// drained before fallback / any-valid), preserving the existing preference stratification.
    /// </summary>
    public bool TryPickAndTake(AntagOptOutSystem optOut, [NotNullWhen(true)] out ICommonSession? session)
    {
        session = null;

        foreach (var pool in orderedPools)
        {
            if (pool.Count == 0)
                continue;

            if (optOut.TryWeightedPickAndTake(pool, out session) && session != null)
                return true;
        }

        return false;
    }

    public int Count => orderedPools.Sum(p => p.Count);
}
