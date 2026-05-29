using System;
using Content.Server._NF.Security.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared.Damage;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._NF.Security.Systems;

public sealed class CdetReactiveLethalSystem : EntitySystem
{
    [Dependency] private readonly BatteryWeaponFireModesSystem _fireModes = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly NPCRetaliationSystem _retaliation = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CdetReactiveLethalComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CdetReactiveLethalComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CdetReactiveLethalComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CdetReactiveLethalComponent, BatteryWeaponFireModesComponent>();

        while (query.MoveNext(out var uid, out var reactive, out var fireModes))
        {
            if (reactive.AlertEndTime is not { } endTime || endTime > now)
                continue;

            reactive.AlertEndTime = null;
            SetFireMode((uid, reactive), fireModes, reactive.NormalFireMode);
        }
    }

    private void OnStartup(Entity<CdetReactiveLethalComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.AlertEndTime = null;

        if (!TryComp<BatteryWeaponFireModesComponent>(ent, out var fireModes))
            return;

        SetFireMode(ent, fireModes, ent.Comp.NormalFireMode);
    }

    private void OnDamageChanged(Entity<CdetReactiveLethalComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        Activate(ent, true, args.Origin);
    }

    private void OnGunRefreshModifiers(Entity<CdetReactiveLethalComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!TryComp<BatteryWeaponFireModesComponent>(ent, out var fireModes))
            return;

        args.SoundGunshot = fireModes.CurrentFireMode == ent.Comp.LethalFireMode
            ? ent.Comp.LethalSound
            : ent.Comp.NonLethalSound;
    }

    private void Activate(Entity<CdetReactiveLethalComponent> ent, bool propagate, EntityUid? attacker = null)
    {
        ent.Comp.AlertEndTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.AlertDuration);

        if (TryComp<BatteryWeaponFireModesComponent>(ent, out var fireModes))
            SetFireMode(ent, fireModes, ent.Comp.LethalFireMode);

        if (attacker is { } target && TryComp<NPCRetaliationComponent>(ent, out var retaliation))
            _retaliation.TryRetaliate((ent.Owner, retaliation), target);

        if (!propagate)
            return;

        foreach (var (otherUid, otherReactive) in _lookup.GetEntitiesInRange<CdetReactiveLethalComponent>(Transform(ent.Owner).Coordinates, ent.Comp.AlertRadius))
        {
            if (otherUid == ent.Owner)
                continue;

            Activate((otherUid, otherReactive), false, attacker);
        }
    }

    private void SetFireMode(Entity<CdetReactiveLethalComponent> ent, BatteryWeaponFireModesComponent fireModes, int mode)
    {
        if (!_fireModes.TrySetFireMode(ent.Owner, fireModes, mode))
            return;

        if (TryComp<GunComponent>(ent, out var gun))
            _gun.RefreshModifiers((ent.Owner, gun));
    }
}