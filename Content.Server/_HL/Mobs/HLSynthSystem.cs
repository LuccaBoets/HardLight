using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Mobs.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Mobs;

public sealed class HLSynthSystem : EntitySystem
{
    private static readonly ProtoId<ReagentPrototype> SynthBloodReagent = "SynthBlood";
    private static readonly ProtoId<ReagentPrototype> NanitesReagent = "Nanites";
    private static readonly string[] BruteTypes = ["Blunt", "Slash", "Piercing"];
    private static readonly string[] BurnTypes = ["Heat", "Shock", "Cold", "Caustic"];

    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HLSynthComponent, MapInitEvent>(OnMapInit, after: [typeof(BloodstreamSystem)]);
        SubscribeLocalEvent<HLSynthComponent, EntityUnpausedEvent>(OnUnpaused);
    }

    private void OnMapInit(Entity<HLSynthComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateInterval;
        ent.Comp.NextHealUpdate = _timing.CurTime + ent.Comp.HealUpdateInterval;

        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream)
            || !_solution.ResolveSolution(ent.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution)
            || bloodstream.BloodSolution is not { } bloodSolutionEntity)
        {
            return;
        }

        var currentNanites = bloodSolution.GetTotalPrototypeQuantity(NanitesReagent);
        var targetNanites = FixedPoint2.Max(FixedPoint2.Zero, ent.Comp.MaxNanites - currentNanites);
        var synthBlood = bloodSolution.GetTotalPrototypeQuantity(SynthBloodReagent);
        var removableBlood = FixedPoint2.Max(FixedPoint2.Zero, synthBlood - ent.Comp.TargetBloodVolume);
        var initialNanites = FixedPoint2.Min(targetNanites, removableBlood);

        if (initialNanites <= FixedPoint2.Zero)
            return;

        _solution.RemoveReagent(bloodSolutionEntity, SynthBloodReagent, initialNanites);
        _solution.TryAddReagent(bloodSolutionEntity, NanitesReagent, initialNanites);
    }

    private void OnUnpaused(Entity<HLSynthComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.NextUpdate += args.PausedTime;
        ent.Comp.NextHealUpdate += args.PausedTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HLSynthComponent, BloodstreamComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var synth, out var bloodstream, out var damageable, out var mobState))
        {
            var doGeneration = _timing.CurTime >= synth.NextUpdate;
            var doHealing = _timing.CurTime >= synth.NextHealUpdate;

            if (!doGeneration && !doHealing)
                continue;

            if (!_solution.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
            {
                continue;
            }

            var dead = _mobState.IsDead(uid, mobState);
            var synthBlood = bloodSolution.GetTotalPrototypeQuantity(SynthBloodReagent);
            var nanites = bloodSolution.GetTotalPrototypeQuantity(NanitesReagent);

            if (doGeneration)
                synth.NextUpdate += synth.UpdateInterval;

            if (doGeneration &&
                !dead &&
                synthBlood - synth.BloodNaniteThreshold >= synth.NaniteGenerationCost &&
                nanites < synth.MaxNanites &&
                bloodstream.BloodSolution is { } bloodSolutionEntity)
            {
                var naniteRoom = synth.MaxNanites - nanites;
                var nanitesToGenerate = FixedPoint2.Min(synth.NaniteGenerationAmount, naniteRoom);
                var generationFraction = nanitesToGenerate / synth.NaniteGenerationAmount;
                var bloodCost = synth.NaniteGenerationCost * generationFraction;

                if (nanitesToGenerate > FixedPoint2.Zero &&
                    bloodCost > FixedPoint2.Zero &&
                    _solution.RemoveReagent(bloodSolutionEntity, SynthBloodReagent, bloodCost))
                {
                    _solution.TryAddReagent(bloodSolutionEntity, NanitesReagent, nanitesToGenerate);
                    synthBlood -= bloodCost;
                    nanites += nanitesToGenerate;
                }
            }

            if (!doHealing)
                continue;

            synth.NextHealUpdate += synth.HealUpdateInterval;

            if (dead || nanites <= FixedPoint2.Zero || bloodstream.BloodSolution is not { } healSolutionEntity)
                continue;

            if (bloodstream.BleedAmount > 0f &&
                synth.BleedSealNaniteCost > FixedPoint2.Zero &&
                synth.BleedSealAmount > 0f)
            {
                var bleedSealSpend = FixedPoint2.Min(nanites, synth.BleedSealNaniteCost);
                var bleedSealFraction = bleedSealSpend / synth.BleedSealNaniteCost;
                var bleedReduction = synth.BleedSealAmount * bleedSealFraction.Float();

                if (bleedReduction > 0f &&
                    _bloodstream.TryModifyBleedAmount(uid, -bleedReduction, bloodstream) &&
                    _solution.RemoveReagent(healSolutionEntity, NanitesReagent, bleedSealSpend))
                {
                    nanites -= bleedSealSpend;
                }
            }

            if (nanites > FixedPoint2.Zero && synth.NaniteHealCost > FixedPoint2.Zero)
            {
                var naniteToSpend = FixedPoint2.Min(nanites, synth.NaniteHealCost);
                var healFraction = naniteToSpend / synth.NaniteHealCost;
                var repair = BuildPassiveRepair(synth, damageable, healFraction);

                if (repair.DamageDict.Count > 0)
                {
                    var delta = _damageable.TryChangeDamage(uid, repair, true, false, damageable);
                    if (delta != null &&
                        delta.DamageDict.Count > 0 &&
                        _solution.RemoveReagent(healSolutionEntity, NanitesReagent, naniteToSpend))
                    {
                        nanites -= naniteToSpend;
                    }
                }
            }

            if (synth.PassiveBloodRestore > FixedPoint2.Zero &&
                nanites >= synth.MaxNanites &&
                synthBlood < synth.TargetBloodVolume)
            {
                var bloodToRestore = FixedPoint2.Min(synth.PassiveBloodRestore, synth.TargetBloodVolume - synthBlood);
                if (bloodToRestore > FixedPoint2.Zero)
                    _solution.TryAddReagent(healSolutionEntity, SynthBloodReagent, bloodToRestore);
            }
        }
    }

    private DamageSpecifier BuildPassiveRepair(HLSynthComponent synth, DamageableComponent damageable, FixedPoint2 healFraction)
    {
        var repair = new DamageSpecifier();
        AddRepairTypes(repair, damageable, BruteTypes, synth.PassiveBruteHeal * healFraction);
        AddRepairTypes(repair, damageable, BurnTypes, synth.PassiveBurnHeal * healFraction);

        return repair;
    }

    private static void AddRepairTypes(DamageSpecifier repair, DamageableComponent damageable, IReadOnlyList<string> damageTypes, FixedPoint2 healBudget)
    {
        if (healBudget <= FixedPoint2.Zero)
            return;

        var remaining = healBudget;
        foreach (var type in damageTypes)
        {
            if (remaining <= FixedPoint2.Zero)
                break;

            if (!damageable.Damage.DamageDict.TryGetValue(type, out var damage) || damage <= FixedPoint2.Zero)
                continue;

            var healAmount = FixedPoint2.Min(remaining, damage);
            repair.DamageDict[type] = -healAmount;
            remaining -= healAmount;
        }
    }
}
