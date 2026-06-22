using Content.Shared.EntityEffects;
using Content.Shared.Animals.Components; // HL: Moved the LewdEggLayingComponent to Shared
using Content.Server.Animals.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects
{
    /// <summary>
    /// Attempts to find a prizeball laying component and triggers its effects
    /// </summary>
    public sealed partial class Redeem : EntityEffect
    {
        public override void Effect(EntityEffectBaseArgs args)
        {
            var entman = args.EntityManager;
            if (entman.TryGetComponent(args.TargetEntity, out PrizeballLayingComponent? egglaying))
            {
                float amt = (args is EntityEffectReagentArgs reagentArgs) ? (float) reagentArgs.Quantity : 1.0f;
                entman.System<PrizeballLayingSystem>().Redeem(args.TargetEntity, amt, egglaying);
            }
        }

        protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
            => Loc.GetString("reagent-effect-guidebook-redeem", ("chance", Probability));
    }
}
