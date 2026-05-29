using Content.Shared.Actions;
using Content.Shared.Abilities.Psionics;
using Content.Shared.Nyanotrasen.Abilities.Psionics;
using Content.Shared.Speech;
using Content.Shared.Stealth.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Damage;
using Content.Server.Mind;
using Content.Shared.Mobs.Systems;
using Content.Server.Popups;
using Content.Server.Psionics;
using Content.Server.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Actions.Events;
using Content.Server.DoAfter;
using Content.Shared.DoAfter;

namespace Content.Server.Abilities.Psionics
{
    public sealed class MindSwapPowerSystem : EntitySystem
    {
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly MindSystem _mindSystem = default!;
        [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MindSwapPowerComponent, MindSwapPowerActionEvent>(OnPowerUsed);
            SubscribeLocalEvent<PsionicComponent, MindSwapPowerDoAfterEvent>(OnDoAfter);
            SubscribeLocalEvent<MindSwappedComponent, MindSwapPowerReturnActionEvent>(OnPowerReturned);
            SubscribeLocalEvent<MindSwappedComponent, DispelledEvent>(OnDispelled);
            SubscribeLocalEvent<MindSwappedComponent, MobStateChangedEvent>(OnMobStateChanged);
            // SubscribeLocalEvent<GhostAttemptHandleEvent>(OnGhostAttempt); // Commented out - missing type
            //
            SubscribeLocalEvent<MindSwappedComponent, ComponentInit>(OnSwapInit);
        }

        private void OnPowerUsed(EntityUid uid, MindSwapPowerComponent component, MindSwapPowerActionEvent args)
        {
            if (!_psionics.OnAttemptPowerUse(args.Performer, "mind swap")
                || !(TryComp<DamageableComponent>(args.Target, out var damageable) && damageable.DamageContainerID == "Biological"))
                return;

            _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, component.UseDelay, new MindSwapPowerDoAfterEvent(), args.Performer, target: args.Target)
            {
                Hidden = true,
                BreakOnDamage = true,
                BreakOnMove = true
            }, out var doAfterId);

            if (TryComp<PsionicComponent>(uid, out var magic))
                magic.DoAfter = doAfterId;

            _psionics.LogPowerUsed(args.Performer, "mind swap");
            args.Handled = true;
        }

        private void OnDoAfter(EntityUid uid, PsionicComponent component, MindSwapPowerDoAfterEvent args)
        {
            if (component is null)
                return;
            component.DoAfter = null;

            if (args.Target is null
                || args.Cancelled)
                return;

            Swap(uid, args.Target.Value);
        }

        private void OnPowerReturned(EntityUid uid, MindSwappedComponent component, MindSwapPowerReturnActionEvent args)
        {
            if (HasComp<PsionicInsulationComponent>(component.OriginalEntity) || HasComp<PsionicInsulationComponent>(uid))
                return;

            if (HasComp<MobStateComponent>(uid) && !_mobStateSystem.IsAlive(uid))
                return;

            // How do we get trapped?
            // 1. Original target doesn't exist
            if (!component.OriginalEntity.IsValid() || Deleted(component.OriginalEntity))
            {
                GetTrapped(uid);
                return;
            }
            // 1. Original target is no longer mindswapped
            if (!TryComp<MindSwappedComponent>(component.OriginalEntity, out var targetMindSwap))
            {
                GetTrapped(uid);
                return;
            }

            // 2. Target has undergone a different mind swap
            if (targetMindSwap.OriginalEntity != uid)
            {
                GetTrapped(uid);
                return;
            }

            // 3. Target is dead
            if (HasComp<MobStateComponent>(component.OriginalEntity) && _mobStateSystem.IsDead(component.OriginalEntity))
            {
                GetTrapped(uid);
                return;
            }

            Swap(uid, component.OriginalEntity, true);
        }

        private void OnDispelled(EntityUid uid, MindSwappedComponent component, DispelledEvent args)
        {
            Swap(uid, component.OriginalEntity, true);
            args.Handled = true;
        }

        private void OnMobStateChanged(EntityUid uid, MindSwappedComponent component, MobStateChangedEvent args)
        {
            if (args.NewMobState == MobState.Dead)
                RemComp<MindSwappedComponent>(uid);
        }

        /* Commented out - missing GhostAttemptHandleEvent type
        private void OnGhostAttempt(GhostAttemptHandleEvent args)
        {
            if (args.Handled)
                return;

            if (!HasComp<MindSwappedComponent>(args.Mind.CurrentEntity))
                return;

            //No idea where the viaCommand went. It's on the internal OnGhostAttempt, but not this layer. Maybe unnecessary.
            //if (!args.viaCommand)
            //    return;

            args.Result = false;
            args.Handled = true;
        }
        */

        private void OnSwapInit(EntityUid uid, MindSwappedComponent component, ComponentInit args)
        {
            EnsureReturnAction(uid, component);
        }

        private void EnsureReturnAction(EntityUid uid, MindSwappedComponent component)
        {
            _actions.AddAction(uid, ref component.MindSwapReturnActionEntity, component.MindSwapReturnActionId);
            _actions.TryGetActionData(component.MindSwapReturnActionEntity, out var actionData);
            if (actionData is { UseDelay: not null })
                _actions.StartUseDelay(component.MindSwapReturnActionEntity);
        }

        public void EnsureReturnAction(EntityUid uid)
        {
            if (TryComp<MindSwappedComponent>(uid, out var component))
                EnsureReturnAction(uid, component);
        }

        public void Swap(EntityUid performer, EntityUid target, bool end = false)
        {
            if (end && (!HasComp<MindSwappedComponent>(performer) || !HasComp<MindSwappedComponent>(target)))
                return;

            MindSwappedComponent? perfComp = null;
            MindSwappedComponent? targetComp = null;

            if (!end)
            {
                perfComp = EnsureComp<MindSwappedComponent>(performer);
                targetComp = EnsureComp<MindSwappedComponent>(target);

                perfComp.OriginalEntity = target;
                targetComp.OriginalEntity = performer;

                // Grant the return action before control transfers so the newly possessed body already has it.
                EnsureReturnAction(performer, perfComp);
                EnsureReturnAction(target, targetComp);
            }

            // Get the minds first. On transfer, they'll be gone.
            EntityUid performerMindId = default;
            EntityUid targetMindId = default;
            MindComponent? performerMind = null;
            MindComponent? targetMind = null;

            // This is here to prevent missing MindContainerComponent Resolve errors.
            if (!_mindSystem.TryGetMind(performer, out performerMindId, out performerMind))
                performerMind = null;

            if (!_mindSystem.TryGetMind(target, out targetMindId, out targetMind))
                targetMind = null;

            // Detach first without creating temporary ghosts so the subsequent swap is stable.
            if (performerMind != null)
                _mindSystem.TransferTo(performerMindId, null, createGhost: false, mind: performerMind);

            if (targetMind != null)
                _mindSystem.TransferTo(targetMindId, null, createGhost: false, mind: targetMind);

            // Do the transfer.
            if (performerMind != null)
                _mindSystem.TransferTo(performerMindId, target, ghostCheckOverride: true, createGhost: false, mind: performerMind);

            if (targetMind != null)
                _mindSystem.TransferTo(targetMindId, performer, ghostCheckOverride: true, createGhost: false, mind: targetMind);

            if (end)
            {
                var performerMindPowerComp = EntityManager.GetComponent<MindSwappedComponent>(performer);
                var targetMindPowerComp = EntityManager.GetComponent<MindSwappedComponent>(target);
                _actions.RemoveAction(performer, performerMindPowerComp.MindSwapReturnActionEntity);
                _actions.RemoveAction(target, targetMindPowerComp.MindSwapReturnActionEntity);

                RemComp<MindSwappedComponent>(performer);
                RemComp<MindSwappedComponent>(target);
                return;
            }
        }

        public void GetTrapped(EntityUid uid)
        {

            _popupSystem.PopupEntity(Loc.GetString("mindswap-trapped"), uid, uid, Shared.Popups.PopupType.LargeCaution);
            var perfComp = EnsureComp<MindSwappedComponent>(uid);
            _actions.RemoveAction(uid, perfComp.MindSwapReturnActionEntity, null);

            if (HasComp<TelegnosticProjectionComponent>(uid))
            {
                RemComp<PsionicallyInvisibleComponent>(uid);
                RemComp<StealthComponent>(uid);
                EnsureComp<SpeechComponent>(uid);
                EnsureComp<DispellableComponent>(uid);
                _metaDataSystem.SetEntityName(uid, Loc.GetString("telegnostic-trapped-entity-name"));
                _metaDataSystem.SetEntityDescription(uid, Loc.GetString("telegnostic-trapped-entity-desc"));
            }
        }
    }
}
