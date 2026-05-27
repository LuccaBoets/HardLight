// VRS: ship licensing tool system — appraisal tool dual-mode (Appraise / License).
using Content.Server._NF.Bank;
using Content.Server.Cargo.Systems;
using Content.Server.Popups;
using Content.Shared._NF.CCVar;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._VRS.Licensing;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Server.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._VRS.Licensing;

/// <summary>
/// Server-side logic for <see cref="ShipLicensingToolComponent"/>.
/// - Use-in-hand cycles Appraise &lt;-&gt; License mode. Entering License mode requires a
///   <see cref="ShuttleDeedComponent"/>-bearing item in another hand; the tool captures that ship.
/// - In License mode, interacting with a <see cref="ShipLicenseRequiredComponent"/> item shows an
///   appraisal + confirm prompt; a second click within <see cref="ShipLicensingToolComponent.ConfirmWindow"/>
///   withdraws the fee and stamps a <see cref="ShipLicenseComponent"/>.
/// </summary>
public sealed class ShipLicensingToolSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipLicensingToolComponent, UseInHandEvent>(OnUseInHand);
        // Run before SharedPriceGunSystem so we can swallow the interaction in License mode for
        // license-required targets (and otherwise let the price gun handle appraisal as usual).
        SubscribeLocalEvent<ShipLicensingToolComponent, AfterInteractEvent>(OnAfterInteract,
            before: new[] { typeof(Content.Shared.Cargo.Systems.SharedPriceGunSystem) });
    }

    private void OnUseInHand(Entity<ShipLicensingToolComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Mode == ShipLicensingToolMode.Appraise)
        {
            if (!TryFindUserDeed(args.User, out var deed))
            {
                _popup.PopupEntity(Loc.GetString("ship-license-tool-no-deed"), ent, args.User);
                args.Handled = true;
                return;
            }

            ent.Comp.Mode = ShipLicensingToolMode.License;
            ent.Comp.BoundShuttleUid = deed.ShuttleUid;
            ent.Comp.BoundShuttleName = deed.ShuttleName;
            ent.Comp.PendingTarget = null;
            Dirty(ent);

            _popup.PopupEntity(
                Loc.GetString("ship-license-tool-mode-license", ("ship", deed.ShuttleName ?? "ship")),
                ent, args.User);
        }
        else
        {
            ent.Comp.Mode = ShipLicensingToolMode.Appraise;
            ent.Comp.BoundShuttleUid = null;
            ent.Comp.BoundShuttleName = null;
            ent.Comp.PendingTarget = null;
            Dirty(ent);

            _popup.PopupEntity(Loc.GetString("ship-license-tool-mode-appraise"), ent, args.User);
        }

        args.Handled = true;
    }

    private bool TryFindUserDeed(EntityUid user, out ShuttleDeedComponent deed)
    {
        deed = default!;
        // Looks at active hand, the entity itself, then the ID inventory slot (which unwraps a PDA's contained ID).
        if (!_idCard.TryFindIdCard(user, out var idCard))
            return false;

        if (!TryComp<ShuttleDeedComponent>(idCard.Owner, out var found) || found.ShuttleUid is null)
            return false;

        deed = found;
        return true;
    }

    private void OnAfterInteract(Entity<ShipLicensingToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (ent.Comp.Mode != ShipLicensingToolMode.License)
            return;

        if (!TryComp<ShipLicenseRequiredComponent>(target, out var req))
            return; // let price gun do appraisal on non-license-required items

        // We are taking this interaction in license mode regardless of outcome.
        args.Handled = true;

        if (ent.Comp.BoundShuttleUid is null)
        {
            _popup.PopupEntity(Loc.GetString("ship-license-tool-no-deed"), target, args.User);
            return;
        }

        if (TryComp<ShipLicenseComponent>(target, out var existing))
        {
            if (existing.OwnerShuttleUid == ent.Comp.BoundShuttleUid)
                _popup.PopupEntity(Loc.GetString("ship-license-already-licensed-same"), target, args.User);
            else
                _popup.PopupEntity(Loc.GetString("ship-license-already-licensed-other"), target, args.User);
            return;
        }

        var fee = CalculateFee(target, req);
        var now = _timing.CurTime;

        // Second click within the confirm window on the same target -> execute.
        if (ent.Comp.PendingTarget == target && now <= ent.Comp.PendingExpires)
        {
            ent.Comp.PendingTarget = null;

            if (!_bank.TryBankWithdraw(args.User, fee))
            {
                _popup.PopupEntity(
                    Loc.GetString("ship-license-cannot-afford", ("fee", fee)),
                    target, args.User);
                return;
            }

            var license = EnsureComp<ShipLicenseComponent>(target);
            license.OwnerShuttleUid = ent.Comp.BoundShuttleUid;
            license.OwnerShuttleName = ent.Comp.BoundShuttleName;
            license.Bootleg = false;
            Dirty(target, license);

            _popup.PopupEntity(
                Loc.GetString("ship-license-stamped",
                    ("ship", ent.Comp.BoundShuttleName ?? "ship"),
                    ("fee", fee)),
                target, args.User);
            _audio.PlayPvs(ent.Comp.StampSound, ent);
            return;
        }

        // First click -> quote + arm confirm.
        ent.Comp.PendingTarget = target;
        ent.Comp.PendingFee = fee;
        ent.Comp.PendingExpires = now + ent.Comp.ConfirmWindow;

        _popup.PopupEntity(
            Loc.GetString("ship-license-tool-confirm",
                ("ship", ent.Comp.BoundShuttleName ?? "ship"),
                ("fee", fee)),
            target, args.User);
        _audio.PlayPvs(ent.Comp.StampSound, ent);
    }

    private int CalculateFee(EntityUid target, ShipLicenseRequiredComponent req)
    {
        var fraction = req.FeeFractionOverride ?? _cfg.GetCVar(NFCCVars.LicensingFeeFraction);
        var appraisal = _pricing.GetPrice(target, includeContents: false, allowSideEffects: false);
        var fee = (int)System.Math.Round(appraisal * fraction);
        return System.Math.Max(req.MinimumFee, fee);
    }
}
