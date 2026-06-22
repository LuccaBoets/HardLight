using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Systems;
using Content.Shared.Storage;
using Content.Shared.Traits.Events;
using Content.Server.Popups;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Content.Shared.Animals.Systems;
using Content.Shared.Animals.Components;

namespace Content.Server.Animals.Systems;

/// <summary>
///     Gives the ability to lay pballs/other things;
///     produces endlessly if the owner does not have a HungerComponent.
/// </summary>
public sealed class PrizeballLayingSystem : SharedPrizeballLayingSystem //  We've changed the base to SharedPrizeballLayingSystem so we can run the Verb drawing on the client.
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrizeballLayingComponent, ComponentShutdown>(OnHostShutdown);
        SubscribeLocalEvent<PrizeballLayingComponent, PrizeballLayingActionEvent>(OnPballLayingAction);
        SubscribeLocalEvent<PrizeballLayingComponent, PrizeballLayingDoAfterEvent>(OnPballLayingDoAfter);
        SubscribeLocalEvent<PrizeballLayingComponent, PrizeballLayingInsideDoAfterEvent>(OnPballLayingInsideDoAfter);
        SubscribeLocalEvent<PrizeballLayingComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    private void OnHostShutdown(EntityUid user, PrizeballLayingComponent pballLaying, ComponentShutdown args)
    {
        _actions.RemoveAction(user, pballLaying.Action);
    }

   protected override void AttemptLayInside(Entity<PrizeballLayingComponent> user, EntityUid target)
    {
        var doargs = new DoAfterArgs(EntityManager, user.Owner, user.Comp.PballLayDelay, new PrizeballLayingInsideDoAfterEvent(), user.Owner, target)
        {
            BreakOnMove = true,
            BlockDuplicate = true,
            BreakOnDamage = true,
            CancelDuplicate = true,
        };

        _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-inside-start", ("entity", Identity.Entity(user.Owner, EntityManager)), ("target", Identity.Entity(target, EntityManager))), user);
        _doAfter.TryStartDoAfter(doargs);
    }

    private void OnRefreshMovespeed(EntityUid user, PrizeballLayingComponent pballLaying, RefreshMovementSpeedModifiersEvent args)
    {
        if (pballLaying.isHeavyOfPballs())
        {
            args.ModifySpeed(pballLaying.PballSlowMult, pballLaying.PballSlowMult);
        }
    }

    private void OnPballLayingAction(EntityUid user, PrizeballLayingComponent pballLaying, PrizeballLayingActionEvent args)
    {
        if (!pballLaying.hasPballs())
        {
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-no-pballs"), user, user);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, pballLaying.PballLayDelay, new PrizeballLayingDoAfterEvent(), user)
        {
            BreakOnMove = true,
            BlockDuplicate = true,
            BreakOnDamage = true,
            CancelDuplicate = true,
        };

        _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-user-start"), user, user);
        _doAfter.TryStartDoAfter(doAfter);
    }

    public void Redeem(EntityUid user, float amount, PrizeballLayingComponent? pballLaying = null)
    {
        if (!Resolve(user, ref pballLaying) || pballLaying.Temporary)
            return;

        amount *= pballLaying.ProductionMult;

        bool hasPballsBefore = pballLaying.hasPballs();
        bool isHeavyBefore = pballLaying.isHeavyOfPballs();
        bool isFullBefore = pballLaying.isFullOfPballs();

        AddPballs(user, pballLaying, amount);

        if(pballLaying.hasPballs() && !hasPballsBefore)
        {
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-firstpball"), user, user);
            _actions.AddAction(user, ref pballLaying.Action, pballLaying.ActionPrototype);
        }
        else if(pballLaying.isHeavyOfPballs() && !isHeavyBefore)
        {
            _movementSpeedModifier.RefreshMovementSpeedModifiers(user);
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-heavypballs"), user, user);
        }
        else if(pballLaying.isFullOfPballs() && !isFullBefore)
        {
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-fullpballs"), user, user);
        }
        else if(pballLaying.doFlavor())
        {
            _popup.PopupEntity(Loc.GetString(_random.Pick(pballLaying.FlavorMessages)), user, user);
        }
    }

    private void OnPballLayingInsideDoAfter(EntityUid user, PrizeballLayingComponent myPballs, PrizeballLayingInsideDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;
            
        args.Handled = true;

        if (myPballs.Deleted || !myPballs.hasPballs())
        {
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-nopballs"), user, user);
            return;
        }
        var target = args.Target.Value;

        _audio.PlayPvs(myPballs.PballLaySound, user);

        if (!TryComp<PrizeballLayingComponent>(target, out var theirPballs))
        {
            theirPballs = (PrizeballLayingComponent)Factory.GetComponent(Factory.GetComponentName<PrizeballLayingComponent>());
            EntityManager.AddComponent(target, theirPballs);
            theirPballs.makeTempFrom(myPballs);
            _actions.AddAction(target, ref theirPballs.Action, theirPballs.ActionPrototype);
        }

        /// HL: Moved the AddPballs to a helper function so we can share the pballs count in the component to the client
        AddPballs(user, myPballs, -1.0f);
        AddPballs(target, theirPballs, 1.0f);

        _movementSpeedModifier.RefreshMovementSpeedModifiers(user);
        _movementSpeedModifier.RefreshMovementSpeedModifiers(target);

        if(myPballs.hasPballs())
        {
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-inside-give-more", ("entity", Identity.Entity(target, EntityManager))), user, user);
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-inside-receive-more", ("entity", Identity.Entity(user, EntityManager))), target, target);
            args.Repeat = true;
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-inside-give-done", ("entity", Identity.Entity(target, EntityManager))), user, user);
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-inside-receive-done", ("entity", Identity.Entity(user, EntityManager))), target, target);

            if(myPballs.Temporary)
                RemComp<PrizeballLayingComponent>(user);
            else
                _actions.RemoveAction(user, myPballs.Action);
        }
    }

    private void OnPballLayingDoAfter(EntityUid user, PrizeballLayingComponent pballLaying, PrizeballLayingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
            
        args.Handled = true;

        if (pballLaying.Deleted || !pballLaying.hasPballs())
        {
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-nopballs"), user, user);
            return;
        }

        foreach (var ent in EntitySpawnCollection.GetSpawns(pballLaying.EggSpawn, _random))
        {
            Spawn(ent, Transform(user).Coordinates);
        }

        _audio.PlayPvs(pballLaying.PballLaySound, user);

        AddPballs(user, pballLaying, -1.0f);
        _movementSpeedModifier.RefreshMovementSpeedModifiers(user);

        if(pballLaying.hasPballs())
        {
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-user-more"), user, user);
            args.Repeat = true;
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-user-done"), user, user);

            if(pballLaying.Temporary)
                EntityManager.RemoveComponent<PrizeballLayingComponent>(user);
            else
                _actions.RemoveAction(user, pballLaying.Action);
        }
        _popup.PopupEntity(Loc.GetString("action-popup-lay-pball-others", ("entity", user)), user, Filter.PvsExcept(user), true);
    }
}
