using Content.Server.Xenoarchaeology.Artifact;
using Content.Shared.EntityEffects;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects;

public sealed partial class ActivateArtifact : EntityEffect
{
    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent<XenoArtifactComponent>(args.TargetEntity, out var artifact))
            return;

        var xenoArtifact = args.EntityManager.EntitySysManager.GetEntitySystem<XenoArtifactSystem>();
        xenoArtifact.TriggerXenoArtifact((args.TargetEntity, artifact), null, true);

        if (args.EntityManager.TryGetComponent<XenoArtifactUnlockingComponent>(args.TargetEntity, out var unlocking))
            xenoArtifact.SetArtifexiumApplied((args.TargetEntity, unlocking), true);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-activate-artifact", ("chance", Probability));
}
