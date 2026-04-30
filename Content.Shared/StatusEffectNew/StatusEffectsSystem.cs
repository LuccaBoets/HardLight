using System.Diagnostics.CodeAnalysis;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.StatusEffectNew;

public sealed class StatusEffectsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private EntityQuery<StatusEffectContainerComponent> _containerQuery;
    private EntityQuery<StatusEffectComponent> _statusQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusEffectContainerComponent, ComponentInit>(OnStatusContainerInit);
        SubscribeLocalEvent<StatusEffectContainerComponent, ComponentShutdown>(OnStatusContainerShutdown);

        _containerQuery = GetEntityQuery<StatusEffectContainerComponent>();
        _statusQuery = GetEntityQuery<StatusEffectComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StatusEffectComponent>();
        while (query.MoveNext(out var uid, out var status))
        {
            if (status.EndEffectTime is not { } endTime)
                continue;

            if (_timing.CurTime < endTime)
                continue;

            PredictedQueueDel(uid);
        }
    }

    private void OnStatusContainerInit(Entity<StatusEffectContainerComponent> ent, ref ComponentInit args)
    {
        ent.Comp.ActiveStatusEffects = _container.EnsureContainer<Container>(ent, StatusEffectContainerComponent.ContainerId);
        ent.Comp.ActiveStatusEffects.ShowContents = true;
        ent.Comp.ActiveStatusEffects.OccludesLight = false;
    }

    private void OnStatusContainerShutdown(Entity<StatusEffectContainerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ActiveStatusEffects is { } container)
            _container.ShutdownContainer(container);
    }

    public bool TryGetStatusEffect(EntityUid target, EntProtoId effectProto, [NotNullWhen(true)] out EntityUid? statusEffect)
    {
        statusEffect = null;

        if (!_containerQuery.TryComp(target, out var containerComp) || containerComp.ActiveStatusEffects == null)
            return false;

        foreach (var contained in containerComp.ActiveStatusEffects.ContainedEntities)
        {
            if (!_statusQuery.TryComp(contained, out var status) || status.AppliedTo != target)
                continue;

            var containedProto = Prototype(contained);
            if (containedProto == null || containedProto != effectProto)
                continue;

            statusEffect = contained;
            return true;
        }

        return false;
    }

    public bool TrySetStatusEffectDuration(EntityUid target, EntProtoId effectProto, TimeSpan? duration)
    {
        return TrySetStatusEffectDuration(target, effectProto, out _, duration);
    }

    public bool TrySetStatusEffectDuration(EntityUid target, EntProtoId effectProto, [NotNullWhen(true)] out EntityUid? statusEffect, TimeSpan? duration = null)
    {
        statusEffect = null;

        if (TryGetStatusEffect(target, effectProto, out statusEffect))
        {
            if (statusEffect == null)
                return false;

            var existingUid = statusEffect.Value;
            if (!_statusQuery.TryComp(existingUid, out var existing))
                return false;

            existing.AppliedTo = target;
            existing.StartEffectTime = _timing.CurTime;
            existing.EndEffectTime = duration == null ? null : _timing.CurTime + duration.Value;
            Dirty(existingUid, existing);
            return true;
        }

        EnsureComp<StatusEffectContainerComponent>(target);

        if (!PredictedTrySpawnInContainer(effectProto, target, StatusEffectContainerComponent.ContainerId, out var spawned))
            return false;

        if (!_statusQuery.TryComp(spawned, out var status))
            return false;

        status.AppliedTo = target;
        status.StartEffectTime = _timing.CurTime;
        status.EndEffectTime = duration == null ? null : _timing.CurTime + duration.Value;
        Dirty(spawned.Value, status);

        statusEffect = spawned;
        return true;
    }

    public bool TryEffectsWithComp<T>(EntityUid target, [NotNullWhen(true)] out HashSet<Entity<T, StatusEffectComponent>>? effects)
        where T : IComponent
    {
        effects = null;

        if (!_containerQuery.TryComp(target, out var containerComp) || containerComp.ActiveStatusEffects == null)
            return false;

        var set = new HashSet<Entity<T, StatusEffectComponent>>();
        foreach (var contained in containerComp.ActiveStatusEffects.ContainedEntities)
        {
            if (!TryComp<T>(contained, out var comp) || !_statusQuery.TryComp(contained, out var status))
                continue;

            if (status.AppliedTo != target)
                continue;

            set.Add((contained, comp, status));
        }

        if (set.Count == 0)
            return false;

        effects = set;
        return true;
    }
}
