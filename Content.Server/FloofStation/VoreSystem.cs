using Robust.Shared.Containers;
using Robust.Shared.Audio.Systems;
using Content.Server.Body.Components;
using Content.Server._Common.Consent;
using Content.Shared.Examine;
using Content.Server.Atmos.Components;
using Content.Server.Temperature.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Damage;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Server.Chat.Managers;
using Content.Server.DoAfter;
using Content.Shared.Popups;
using Robust.Server.Player;
using Content.Shared.Mobs.Systems;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.FloofStation;
using Robust.Shared.Random;
using Content.Shared.Inventory;
using Robust.Shared.Physics.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Shared.PowerCell.Components;
using System.Linq;
using Content.Shared.Contests;
using Content.Shared.Standing;
using Content.Server.Power.Components;
using Content.Server.Nutrition.EntitySystems;
using Content.Shared.Mind.Components;
using Content.Server._Starlight.NullSpace;
using Content.Shared._Starlight.NullSpace;

namespace Content.Server.FloofStation;

public sealed class VoreSystem : SharedVoreSystem // HL: Changed the base to Shared so the client can handle verb drawing
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly ConsentSystem _consent = default!;
    [Dependency] private readonly BlindableSystem _blindableSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ContestsSystem _contests = default!;
    [Dependency] private readonly StandingStateSystem _standingState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly FoodSystem _food = default!;
    [Dependency] private readonly NullSpacePhaseSystem _phase = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoreComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<VoreComponent, BeingGibbedEvent>(OnGibContents);
        SubscribeLocalEvent<VoreComponent, ExaminedEvent>((uid, _, args) => OnExamine(uid, args));
        SubscribeLocalEvent<VoreComponent, VoreDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<VoredComponent, EntGotRemovedFromContainerMessage>(OnRelease);
        SubscribeLocalEvent<VoredComponent, CanSeeAttemptEvent>(OnSeeAttempt);
    }

    private void OnInit(EntityUid uid, VoreComponent component, MapInitEvent args)
    {
        component.Stomach = _containerSystem.EnsureContainer<Container>(uid, "stomach");
    }

    public override void TryDevour(EntityUid uid, EntityUid target, VoreComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (_food.IsMouthBlocked(uid, uid))
            return;

        _popups.PopupEntity(Loc.GetString("vore-attempt-devour", ("entity", uid), ("prey", target)), uid, PopupType.LargeCaution);

        if (!TryComp<PhysicsComponent>(uid, out var predPhysics)
            || !TryComp<PhysicsComponent>(target, out var preyPhysics))
            return;

        var length = TimeSpan.FromSeconds(component.Delay
                        * _contests.MassContest(preyPhysics, predPhysics, false, 4f)
                        * _contests.StaminaContest(uid, target)
                        * (_standingState.IsDown(target) ? 0.5f : 1));

        if (HasComp<NullSpaceComponent>(uid))
        {
            _popups.PopupEntity(Loc.GetString("vore-attempt-phasenom", ("prey", target)), target, PopupType.LargeCaution);

            _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, length, new VoreDoAfterEvent(), uid, target: target)
            {
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
                RequireCanInteract = false
            });
        }
        else
        {
            _popups.PopupEntity(Loc.GetString("vore-attempt-devour", ("entity", uid), ("prey", target)), uid, PopupType.LargeCaution);

            _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, length, new VoreDoAfterEvent(), uid, target: target)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                RequireCanInteract = true
            });
        }
    }

    private void OnDoAfter(EntityUid uid, VoreComponent component, VoreDoAfterEvent args)
    {
        if (component is null)
            return;

        if (args.Target is null
            || args.Cancelled)
            return;

        Devour(uid, args.Target.Value, component);
    }

    public void Devour(EntityUid uid, EntityUid target, VoreComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (HasComp<NullSpaceComponent>(uid))
        {
            _transform.SetWorldPositionRotation(uid, _transform.GetWorldPositionRotation(target).WorldPosition, _transform.GetWorldPositionRotation(target).WorldRotation);
            _phase.Phase(uid);
        }

        var vored = EnsureComp<VoredComponent>(target);
        vored.Pred = uid;
        EnsureComp<PressureImmunityComponent>(target);
        // EnsureComp<RespiratorImmuneComponent>(target);
        _blindableSystem.UpdateIsBlind(target);
        if (TryComp<TemperatureComponent>(target, out var temp))
            temp.AtmosTemperatureTransferEfficiency = 0;

        _containerSystem.Insert(target, component.Stomach);
        _audioSystem.PlayPvs(component.SoundDevour, uid);

        if (_playerManager.TryGetSessionByEntity(target, out var sessionprey)
            || sessionprey is not null)
            _audioSystem.PlayEntity(component.SoundDevour, sessionprey, uid);

        if (_playerManager.TryGetSessionByEntity(uid, out var sessionpred)
            || sessionpred is not null)
        {
            _audioSystem.PlayEntity(component.SoundDevour, sessionpred, uid);
            // var message = Loc.GetString("", ("entity", uid));
            // _chatManager.ChatMessageToOne(
            //     ChatChannel.Emotes,
            //     message,
            //     message,
            //     EntityUid.Invalid,
            //     false,
            //     sessionprey.Channel);
        }

        _popups.PopupEntity(Loc.GetString("vore-devoured", ("entity", uid), ("prey", target)), target, target, PopupType.SmallCaution);
        _popups.PopupEntity(Loc.GetString("vore-devoured", ("entity", uid), ("prey", target)), target, uid, PopupType.SmallCaution);

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(uid)} vored {ToPrettyString(target)}");
    }

    private void OnRelease(EntityUid uid, VoredComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (!TryComp<VoreComponent>(component.Pred, out var predvore)
            || predvore.Stomach != args.Container)
            return;

        // HardLight: If digestion has already completed, enforce deletion instead of release behavior.
        if (component.Digesting && _mobState.IsDead(uid))
        {
            QueueDel(uid);
            return;
        }

        _transform.AttachToGridOrMap(uid);

        RemComp<VoredComponent>(uid);
        RemComp<PressureImmunityComponent>(uid);
        // RemComp<RespiratorImmuneComponent>(uid);
        _blindableSystem.UpdateIsBlind(uid);
        if (TryComp<TemperatureComponent>(uid, out var temp))
            temp.AtmosTemperatureTransferEfficiency = 0.1f;

        if (_playerManager.TryGetSessionByEntity(args.Container.Owner, out var sessionpred)
            || sessionpred is not null)
            _audioSystem.PlayEntity(component.SoundRelease, sessionpred, uid);

        if (_playerManager.TryGetSessionByEntity(uid, out var sessionprey)
            || sessionprey is not null)
            _audioSystem.PlayEntity(component.SoundRelease, sessionprey, uid);

        _popups.PopupEntity(Loc.GetString("vore-released", ("entity", uid), ("pred", args.Container.Owner)), uid, args.Container.Owner, PopupType.Medium);
        _popups.PopupEntity(Loc.GetString("vore-released", ("entity", uid), ("pred", args.Container.Owner)), uid, uid, PopupType.Medium);

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(uid)} got released from {ToPrettyString(args.Container.Owner)} belly");
    }

    public override void ReleasePrey(EntityUid uid, VoredComponent? compnent = null) // HL: Moved this from the verb code to here so the client doesn't need the containerSystem
    {
        _containerSystem.TryRemoveFromContainer(uid, true);
    }

    public override void Digest(EntityUid uid, VoredComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(component.Pred)} started digesting {ToPrettyString(uid)}");

        component.Digesting = true;

        _popups.PopupEntity(Loc.GetString("vore-digest-start", ("entity", component.Pred)), component.Pred, component.Pred, PopupType.LargeCaution);
        if (_playerManager.TryGetSessionByEntity(component.Pred, out var sessionpred)
            || sessionpred is not null)
        {
            var message = Loc.GetString("vore-digest-start-chat", ("entity", component.Pred));
            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                message,
                message,
                EntityUid.Invalid,
                false,
                sessionpred.Channel);
        }

        _popups.PopupEntity(Loc.GetString("vore-digest-start", ("entity", component.Pred)), component.Pred, uid, PopupType.LargeCaution);
        if (_playerManager.TryGetSessionByEntity(uid, out var sessionprey)
            || sessionprey is not null)
        {
            var message = Loc.GetString("vore-digest-start-chat", ("entity", component.Pred));
            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                message,
                message,
                EntityUid.Invalid,
                false,
                sessionprey.Channel);
        }
    }

    public override void StopDigest(EntityUid uid, VoredComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(component.Pred)} stopped digesting {ToPrettyString(uid)}");

        component.Digesting = false;

        _popups.PopupEntity(Loc.GetString("vore-digest-stop", ("entity", component.Pred)), component.Pred, component.Pred, PopupType.Large);
        if (_playerManager.TryGetSessionByEntity(component.Pred, out var sessionpred)
            || sessionpred is not null)
        {
            var message = Loc.GetString("vore-digest-stop", ("entity", component.Pred));
            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                message,
                message,
                EntityUid.Invalid,
                false,
                sessionpred.Channel);
        }

        _popups.PopupEntity(Loc.GetString("vore-digest-stop", ("entity", component.Pred)), component.Pred, uid, PopupType.Large);
        if (_playerManager.TryGetSessionByEntity(uid, out var sessionprey)
            || sessionprey is not null)
        {
            var message = Loc.GetString("vore-digest-stop", ("entity", component.Pred));
            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                message,
                message,
                EntityUid.Invalid,
                false,
                sessionprey.Channel);
        }
    }

    private void FullyDigest(EntityUid uid, EntityUid prey)
    {
        _adminLog.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(uid)} fully digested {ToPrettyString(prey)}");

        var digestedmessage = _random.Next(1, 8);

        if (_playerManager.TryGetSessionByEntity(uid, out var sessionpred)
            || sessionpred is not null)
        {
            var message = Loc.GetString("vore-digested-owner-" + digestedmessage, ("entity", prey));
            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                message,
                message,
                EntityUid.Invalid,
                false,
                sessionpred.Channel);
        }

        if (_playerManager.TryGetSessionByEntity(prey, out var sessionprey)
            || sessionprey is not null)
        {
            var message = Loc.GetString("vore-digested-prey-" + digestedmessage, ("entity", uid));
            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                message,
                message,
                EntityUid.Invalid,
                false,
                sessionprey.Channel);
        }

        if (TryComp<InventoryComponent>(prey, out var inventoryComponent)
            && _inventorySystem.TryGetSlots(prey, out var slots)
            && TryComp<MindContainerComponent>(prey, out var mindContainer)
            && mindContainer.HasMind) // no more digesting wizards to get their panties
        {
            foreach (var slot in slots)
            {
                if (_inventorySystem.TryGetSlotEntity(
                        prey,
                        slot.Name,
                        out var item,
                        inventoryComponent))
                {
                    // if (TryComp<DnaComponent>(uid, out var dna))
                    // {
                    //     var partComp = EnsureComp<ForensicsComponent>(item.Value);
                    //     partComp.DNAs.Add(dna.DNA);
                    //     Dirty(item.Value, partComp);
                    // }
                    _transform.AttachToGridOrMap(item.Value);
                }
            }
        }

        if (TryComp<VoreComponent>(prey, out var preyvore))
            _containerSystem.EmptyContainer(preyvore.Stomach);

        QueueDel(prey);
    }

    private void OnExamine(EntityUid uid, ExaminedEvent args)
    {
        if (!_containerSystem.TryGetContainer(uid, "stomach", out var stomach)
            || stomach.ContainedEntities.Count < 1)
            return;

        // Check if the entity being examined has ShowOnExamine enabled
        if (!TryComp<VoreComponent>(uid, out var voreComp) || !voreComp.ShowOnExamine)
            return;

        args.PushMarkup(Loc.GetString("vore-examine", ("count", stomach.ContainedEntities.Count)), -1);
    }

    private void OnSeeAttempt(EntityUid uid, VoredComponent component, CanSeeAttemptEvent args)
    {
        if (component.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    private void OnGibContents(EntityUid uid, VoreComponent component, ref BeingGibbedEvent args)
    {
        _containerSystem.EmptyContainer(component.Stomach);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<VoredComponent>();
        while (query.MoveNext(out var uid, out var vored))
        {
            if (!vored.Digesting)
                continue;

            vored.Accumulator += frameTime;

            if (vored.Accumulator <= 1)
                continue;

            vored.Accumulator -= 1;

            if (!_consent.HasConsent(uid, "Digestion"))
            {
                StopDigest(uid, vored);
                continue;
            }

            if (_mobState.IsDead(uid))
            {
                FullyDigest(vored.Pred, uid);
                continue;
            }
            else
            {
                DamageSpecifier damage = new();
                damage.DamageDict.Add("Caustic", 1);
                _damageable.TryChangeDamage(uid, damage, true, false);

                // Give 1 Hunger per 1 Caustic Damage.
                if (TryComp<HungerComponent>(vored.Pred, out var hunger))
                    _hunger.ModifyHunger(vored.Pred, 1, hunger);

                // Give 2 Power per 1 Caustic Damage.
                if (TryComp<BatteryComponent>(vored.Pred, out var internalbattery))
                    _battery.SetCharge(vored.Pred, internalbattery.CurrentCharge + 2, internalbattery);

                // Give 2 Power per 1 Caustic Damage.
                if (TryComp<PowerCellSlotComponent>(vored.Pred, out var batterySlot)
                    && _containerSystem.TryGetContainer(vored.Pred, batterySlot.CellSlotId, out var container)
                    && container.ContainedEntities.Count > 0)
                {
                    var battery = container.ContainedEntities.First();
                    if (TryComp<BatteryComponent>(battery, out var batterycomp))
                        _battery.SetCharge(battery, batterycomp.CurrentCharge + 2, batterycomp);
                }
            }
        }
    }
}
