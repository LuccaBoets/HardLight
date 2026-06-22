using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Mobs.Components;
using Content.Shared._HL.Chemistry.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._HL.Chemistry.EntitySystems;

public sealed class ReagentSpoilageSystem : EntitySystem
{
    private readonly record struct SpoilageKey(EntityUid Owner, EntityUid Solution, ReagentId Reagent);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private readonly HashSet<EntityUid> _processing = [];
    private readonly Dictionary<SpoilageKey, TimeSpan> _pendingSpoilage = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetaDataComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingSpoilage.Count == 0)
            return;

        foreach (var (key, spoilAt) in _pendingSpoilage.ToArray())
        {
            if (_timing.CurTime < spoilAt)
                continue;

            TryProcessPendingSpoilage(key);
        }
    }

    private void OnSolutionChanged(Entity<MetaDataComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (_processing.Contains(ent.Owner) ||
            !TryComp<SolutionContainerManagerComponent>(ent, out var manager))
        {
            return;
        }

        Entity<SolutionComponent>? solutionEntity = null;
        if (!_solution.ResolveSolution((ent.Owner, (SolutionContainerManagerComponent?) manager), args.SolutionId, ref solutionEntity, out var solution))
            return;

        _processing.Add(ent.Owner);

        var seenSpoilage = new HashSet<SpoilageKey>();

        foreach (var reagentQuantity in solution.Contents.ToArray())
        {
            var reagentProto = _prototype.Index<ReagentPrototype>(reagentQuantity.Reagent.Prototype);
            var spoiled = reagentProto.SpoilConditions?.SpoilsInto;
            if (spoiled is not { })
            {
                continue;
            }

            var key = new SpoilageKey(ent.Owner, solutionEntity!.Value.Owner, reagentQuantity.Reagent);
            seenSpoilage.Add(key);

            if (IsValidSpoilageContext(ent.Owner, reagentProto))
            {
                _pendingSpoilage.Remove(key);
                continue;
            }

            if (reagentProto.SpoilConditions?.SpoilTime > TimeSpan.Zero)
            {
                _pendingSpoilage[key] = _timing.CurTime + reagentProto.SpoilConditions!.SpoilTime;
                continue;
            }

            _solution.RemoveReagent(solutionEntity.Value, reagentQuantity);
            _solution.TryAddReagent(solutionEntity.Value, spoiled, reagentQuantity.Quantity);
            _pendingSpoilage.Remove(key);
        }

        foreach (var key in _pendingSpoilage.Keys.Where(x => x.Owner == ent.Owner && x.Solution == solutionEntity!.Value.Owner).ToArray())
        {
            if (!seenSpoilage.Contains(key))
                _pendingSpoilage.Remove(key);
        }

        _processing.Remove(ent.Owner);
    }

    private void TryProcessPendingSpoilage(SpoilageKey key)
    {
        if (_processing.Contains(key.Owner) ||
            !EntityManager.EntityExists(key.Owner) ||
            !TryComp<SolutionComponent>(key.Solution, out var solutionComp))
        {
            _pendingSpoilage.Remove(key);
            return;
        }

        Entity<SolutionComponent> solutionEntity = (key.Solution, solutionComp);
        var solution = solutionEntity.Comp.Solution;
        var current = solution.Contents.FirstOrDefault(x => x.Reagent == key.Reagent);

        if (current.Quantity <= 0)
        {
            _pendingSpoilage.Remove(key);
            return;
        }

        var reagentProto = _prototype.Index<ReagentPrototype>(key.Reagent.Prototype);
        var spoiled = reagentProto.SpoilConditions?.SpoilsInto;
        if (spoiled is not { } || IsValidSpoilageContext(key.Owner, reagentProto))
        {
            _pendingSpoilage.Remove(key);
            return;
        }

        _processing.Add(key.Owner);
        _solution.RemoveReagent(solutionEntity, current);
        _solution.TryAddReagent(solutionEntity, spoiled, current.Quantity);
        _processing.Remove(key.Owner);
        _pendingSpoilage.Remove(key);
    }

    private bool IsValidSpoilageContext(EntityUid owner, ReagentPrototype proto)
    {
        if (HasComp<HLSynthComponent>(owner))
            return true;

        if ((proto.SpoilConditions?.PreservedBySpoilageContainers ?? true) && HasComp<PreservesSpoilageComponent>(owner))
            return true;

        if (proto.SpoilConditions == null)
            return false;

        if (TryComp<BloodstreamComponent>(owner, out var bloodstream))
            return !proto.SpoilConditions.ShouldSpoil(true, bloodstream.BloodReagent);

        return !proto.SpoilConditions.ShouldSpoil(false, null);
    }
}
