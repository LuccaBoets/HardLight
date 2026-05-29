using Content.Shared._HL.Aphrodisiac;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._HL.EntityEffects.Effects;

public sealed partial class Aphrodisiac : EntityEffect
{
    /// <summary>
    ///     AphrodisiacPower is how long each metabolism cycle will make the drunk effect last for.
    /// </summary>
    [DataField]
    public TimeSpan AphrodisiacPower = TimeSpan.FromSeconds(3f);

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-aphrodisiac", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        var aphrodisiacPower = AphrodisiacPower;

        if (args is EntityEffectReagentArgs reagentArgs)
            aphrodisiacPower *= reagentArgs.Scale.Float();

        var aphrodisiacSys = args.EntityManager.EntitySysManager.GetEntitySystem<SharedAphrodisiacSystem>();
        aphrodisiacSys.TryApplyAphrodisiacs(args.TargetEntity, aphrodisiacPower);
    }
}
