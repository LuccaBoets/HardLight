using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Devour;
using Content.Shared.Devour.Components;
using Content.Shared.Mobs.Components;

namespace Content.Server.Devour;

public sealed class DevourSystem : SharedDevourSystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevourerComponent, DevourDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DevourerComponent, BeingGibbedEvent>(OnGibContents);
    }

    private void OnDoAfter(EntityUid uid, DevourerComponent component, DevourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        var ichorInjection = new Solution(component.Chemical, component.HealRate);
        var target = args.Args.Target;
        var targetIsCreature = false;

        if (target is EntityUid targetUid && HasComp<MobStateComponent>(targetUid))
        {
            targetIsCreature = true;
            ichorInjection.ScaleSolution(0.5f);

            if (component.ShouldStoreDevoured)
            {
                ContainerSystem.Insert(targetUid, component.Stomach);
            }
        }

        if (target is EntityUid targetEntity)
        {
            _bloodstreamSystem.TryAddToChemicals(uid, ichorInjection);

            if (!targetIsCreature)
            {
                QueueDel(targetEntity);
            }
        }

        _audioSystem.PlayPvs(component.SoundDevour, uid);
    }

    private void OnGibContents(EntityUid uid, DevourerComponent component, ref BeingGibbedEvent args)
    {
        if (!component.ShouldStoreDevoured)
            return;

        // For some reason we have two different systems that should handle gibbing,
        // and for some another reason GibbingSystem, which should empty all containers, doesn't get involved in this process
        ContainerSystem.EmptyContainer(component.Stomach);
    }
}

