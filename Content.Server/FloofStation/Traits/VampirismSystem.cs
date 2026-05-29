using Content.Server.Body.Components;
using Content.Server.Damage.Systems;
using Content.Server.Floofstation.Traits.Components;
using Content.Server.Vampiric;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Floofstation.Traits;

public sealed class VampirismSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VampirismComponent, MapInitEvent>(OnInitVampire);
        SubscribeLocalEvent<VampirismComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<VampirismComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<VampirismComponent, DamageModifyEvent>(OnDamageModify);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<VampirismComponent, HungerComponent>();
        while (query.MoveNext(out var uid, out var vampComp, out var hunger))
        {
            if (vampComp.OverfedRegenAmount <= 0 || (hunger.CurrentThreshold != HungerThreshold.Okay && hunger.CurrentThreshold != HungerThreshold.Overfed))
                continue;

            var healAmount = FixedPoint2.New(-vampComp.OverfedRegenAmount * frameTime);

            if (TryComp<DamageableComponent>(uid, out var damageable))
            {
                _damageableSystem.ChangeAllDamage(uid, damageable, healAmount);
                continue;
            }

            if (TryComp<BodyComponent>(uid, out var body))
            {
                foreach (var (partUid, _) in _body.GetBodyChildren(uid, body))
                {
                    if (!TryComp<DamageableComponent>(partUid, out var partDamageable))
                        continue;

                    _damageableSystem.ChangeAllDamage(partUid, partDamageable, healAmount);
                }
            }
        }
    }

    private void OnGetMeleeDamage(EntityUid uid, VampirismComponent component, ref GetMeleeDamageEvent args)
    {
        if (args.Weapon != uid)
            return;

        if (component.UnarmedDamageMultiplier <= 1f)
            return;

        args.Damage *= component.UnarmedDamageMultiplier;
    }

    private void OnInitVampire(Entity<VampirismComponent> ent, ref MapInitEvent args)
    {
        EnsureBloodSucker(ent);

        if (!TryComp<BodyComponent>(ent, out var body)
		    || !_body.TryGetBodyOrganEntityComps<MetabolizerComponent>((ent, body), out var comps))
            return;

        foreach (var (organUid, metabolizer, organ) in comps)
        {
            if (!TryComp<StomachComponent>(organUid, out var stomach))
                continue;

            metabolizer.MetabolizerTypes = ent.Comp.MetabolizerPrototypes;

            if (ent.Comp.SpecialDigestible is {} whitelist)
                stomach.SpecialDigestible = whitelist;
        }
    }

    private void EnsureBloodSucker(Entity<VampirismComponent> uid)
    {
        if (HasComp<BloodSuckerComponent>(uid))
            return;

        AddComp(uid, new BloodSuckerComponent
        {
            Delay = uid.Comp.SuccDelay,
            UnitsToSucc = uid.Comp.UnitsToSucc,
        });
    }

    private void OnRefreshMovementSpeedModifiers(EntityUid uid, VampirismComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.PeckishWalkMultiplier >= 1f && component.PeckishSprintMultiplier >= 1f &&
            component.StarvingWalkMultiplier >= 1f && component.StarvingSprintMultiplier >= 1f)
        {
            return;
        }

        if (!TryComp<HungerComponent>(uid, out var hunger))
            return;

        switch (hunger.CurrentThreshold)
        {
            case HungerThreshold.Overfed:
                args.ModifySpeed(component.OverfedWalkMultiplier, component.OverfedSprintMultiplier);
                break;
            case HungerThreshold.Okay:
                return;
            case HungerThreshold.Peckish:
                args.ModifySpeed(component.PeckishWalkMultiplier, component.PeckishSprintMultiplier);
                break;
            case HungerThreshold.Starving:
            case HungerThreshold.Dead:
                args.ModifySpeed(component.StarvingWalkMultiplier, component.StarvingSprintMultiplier);
                break;
            default:
                return;
        }
    }

    private void OnDamageModify(EntityUid uid, VampirismComponent component, DamageModifyEvent args)
    {
        if (!TryComp<HungerComponent>(uid, out var hunger))
            return;

        // Increase damage taken when starving
        if (hunger.CurrentThreshold == HungerThreshold.Starving || hunger.CurrentThreshold == HungerThreshold.Dead)
        {
            args.Damage *= component.StarvingDamageMultiplier;
        }
    }
}
