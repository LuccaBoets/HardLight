// VRS: ship-bound licensing — anchor blocker + emag bootleg path.
// Licensing itself is performed via the appraisal tool (see ShipLicensingToolSystem).
using Content.Server.Construction; // VRS: construction-complete hook
using Content.Server.Popups;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.CCVar;
using Content.Shared._VRS.Licensing;
using Content.Shared.Construction.Components;
using Content.Shared.Contraband;
using Content.Shared.Emag.Systems;
using Robust.Shared.Configuration;

namespace Content.Server._VRS.Licensing;

/// <summary>
/// Implements ship-bound licensing for items tagged with <see cref="ShipLicenseRequiredComponent"/>.
/// - Blocks anchoring of unlicensed or wrongly-licensed items via <see cref="AnchorAttemptEvent"/>.
/// - Auto-licenses flatpacked / manually-constructed machines on first anchor to a player ship.
/// - Handles <see cref="GotEmaggedEvent"/> to produce a bootleg license + contraband flag.
/// </summary>
public sealed class ShipLicensingSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipLicenseRequiredComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<ShipLicenseRequiredComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<ShipLicenseRequiredComponent, GotEmaggedEvent>(OnEmagged);

        // Auto-flag freshly-constructed (manual machine frame -> machine) entities for auto-licensing.
        SubscribeLocalEvent<ConstructionChangeEntityEvent>(OnConstructionChangeEntity);
    }

    private void OnConstructionChangeEntity(ConstructionChangeEntityEvent ev)
    {
        var newUid = ev.New;
        if (!HasComp<ShipLicenseRequiredComponent>(newUid))
            return;
        if (HasComp<ShipLicenseComponent>(newUid))
            return;
        EnsureComp<ShipLicensePendingComponent>(newUid);
    }

    private void OnAnchorStateChanged(Entity<ShipLicenseRequiredComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            return;

        if (!TryComp<ShipLicensePendingComponent>(ent, out _))
            return;

        // Pending always consumed on first anchor, regardless of whether we end up stamping.
        RemComp<ShipLicensePendingComponent>(ent);

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid)
            return;

        if (!TryComp<ShuttleDeedComponent>(gridUid, out var gridDeed) || gridDeed.ShuttleUid is null)
            return; // anchored on a station / non-ship grid: nothing to license against.

        var license = EnsureComp<ShipLicenseComponent>(ent);
        license.OwnerShuttleUid = gridDeed.ShuttleUid;
        license.OwnerShuttleName = gridDeed.ShuttleName;
        license.Bootleg = false;
        Dirty(ent.Owner, license);
    }

    private void OnAnchorAttempt(Entity<ShipLicenseRequiredComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_cfg.GetCVar(NFCCVars.LicensingRequireForAnchor))
            return;

        var xform = Transform(ent);
        var gridUid = xform.GridUid;

        // Allow anchoring in space / on non-grid contexts (rare; lets construction tests still work).
        if (gridUid == null)
            return;

        // Station grids (no ShuttleDeedComponent on the grid) are unrestricted — players can still
        // anchor on stations. Licensing only restricts player-owned ships.
        if (!TryComp<ShuttleDeedComponent>(gridUid.Value, out var gridDeed))
            return;

        // Freshly-built (flatpack/construction) machines auto-license on first anchor — let them through.
        if (HasComp<ShipLicensePendingComponent>(ent))
            return;

        if (!TryComp<ShipLicenseComponent>(ent, out var license))
        {
            _popup.PopupEntity(Loc.GetString("ship-license-required-unlicensed"), ent, args.User);
            args.Cancel();
            return;
        }

        if (license.Bootleg)
            return; // bootleg bypasses ship check

        if (license.OwnerShuttleUid is null || gridDeed.ShuttleUid is null ||
            license.OwnerShuttleUid.Value != gridDeed.ShuttleUid.Value)
        {
            _popup.PopupEntity(
                Loc.GetString("ship-license-required-wrong-ship",
                    ("ship", license.OwnerShuttleName ?? "another ship")),
                ent, args.User);
            args.Cancel();
        }
    }

    private void OnEmagged(Entity<ShipLicenseRequiredComponent> ent, ref GotEmaggedEvent args)
    {
        if (args.Type != EmagType.Interaction)
            return;

        if (TryComp<ShipLicenseComponent>(ent, out var existing) && existing.Bootleg)
            return; // already bootlegged

        var license = EnsureComp<ShipLicenseComponent>(ent);
        license.OwnerShuttleUid = null;
        license.OwnerShuttleName = null;
        license.Bootleg = true;
        Dirty(ent, license);

        var contraband = EnsureComp<ContrabandComponent>(ent);
        contraband.Severity = "Restricted";
        Dirty(ent, contraband);

        args.Handled = true;
    }
}
