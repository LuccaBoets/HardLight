using Content.Shared._Common.Consent;
using Content.Shared._Starlight.NullSpace;
using Content.Shared.Damage;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared.FloofStation;

/*
    HL
    Moved VoreSystem to a shared system so that the AddVerb can be called clientside to deal with lag when right-clicking objects/players.
    VoreComponent is now shared, but don't have any networked vars as it's not needed for what we're doing here.
    The VoreSystem on the Server is mostly handling everything, with an empty class of the same name on the Client so that the client can run the AddVerb code locally.
*/
public abstract class SharedVoreSystem : EntitySystem
{
    [Dependency] private readonly SharedConsentSystem _consent = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoreComponent, GetVerbsEvent<InnateVerb>>(AddVerbs);
    }

    private void AddVerbs(EntityUid uid, VoreComponent component, GetVerbsEvent<InnateVerb> args)
    {
        DevourVerb(uid, component, args);
        PhaseNomVerb(uid, component, args);
        VoreVerb(uid, component, args);
    }

    private void PhaseNomVerb(EntityUid uid, VoreComponent component, GetVerbsEvent<InnateVerb> args)
    {
        if (!HasComp<NullSpaceComponent>(uid)
            || HasComp<NullSpaceComponent>(args.Target)
            || args.User == args.Target
            || !HasComp<VoreComponent>(args.Target)
            || !_consent.HasConsent(args.Target, "Vore")
            || !_consent.HasConsent(args.User, "Vore")
            || HasComp<VoredComponent>(args.User))
            return;

        InnateVerb verbDevour = new()
        {
            Act = () => TryDevour(uid, args.Target, component),
            Text = Loc.GetString("vore-devour"),
            Category = VerbCategory.Vore,
            Icon = new SpriteSpecifier.Rsi(new ResPath("Interface/Actions/devour.rsi"), "icon-on"),
            Priority = -1
        };
        args.Verbs.Add(verbDevour);
    }

    private void DevourVerb(EntityUid uid, VoreComponent component, GetVerbsEvent<InnateVerb> args)
    {
        if (!args.CanInteract
            || !args.CanAccess
            || args.User == args.Target
            || !HasComp<VoreComponent>(args.Target)
            || !_consent.HasConsent(args.Target, "Vore")
            || !_consent.HasConsent(args.User, "Vore")
            || HasComp<VoredComponent>(args.User))
            return;

        InnateVerb verbDevour = new()
        {
            Act = () => TryDevour(uid, args.Target, component),
            Text = Loc.GetString("vore-devour"),
            Category = VerbCategory.Vore,
            Icon = new SpriteSpecifier.Rsi(new ResPath("Interface/Actions/devour.rsi"), "icon-on"),
            Priority = -1
        };
        args.Verbs.Add(verbDevour);
    }

    private void VoreVerb(EntityUid uid, VoreComponent component, GetVerbsEvent<InnateVerb> args)
    {
        if (args.User != args.Target)
            return;

        // Add toggle for showing examine text
        if (component.ShowOnExamine)
        {
            InnateVerb verbHideExamine = new()
            {
                Act = () => component.ShowOnExamine = false,
                Text = Loc.GetString("vore-show-examine-on"),
                Category = VerbCategory.Vore,
                Priority = 0,
                Message = "Will show to bystanders examine text that suggests you've consumed people"
            };
            args.Verbs.Add(verbHideExamine);
        }
        else
        {
            InnateVerb verbShowExamine = new()
            {
                Act = () => component.ShowOnExamine = true,
                Text = Loc.GetString("vore-show-examine-off"),
                Category = VerbCategory.Vore,
                Priority = 0,
                Message = "Will show to bystanders examine text that suggests you've consumed people"
            };
            args.Verbs.Add(verbShowExamine);
        }

        if (!_netManager.IsServer)
            return;

        foreach (var prey in component.Stomach.ContainedEntities)
        {
            InnateVerb verbRelease = new()
            {
                Act = () => ReleasePrey(prey),
                Text = Loc.GetString("vore-release", ("entity", prey)),
                Category = VerbCategory.Vore,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                Priority = 2
            };
            args.Verbs.Add(verbRelease);

            if (!TryComp<VoredComponent>(prey, out var vored))
                return;

            if (_consent.HasConsent(prey, "Digestion")
                && HasComp<DamageableComponent>(args.Target)
                && !vored.Digesting)
            {
                InnateVerb verbDigest = new()
                {
                    Act = () => Digest(prey),
                    Text = Loc.GetString("vore-digest", ("entity", prey)),
                    Category = VerbCategory.Vore,
                    Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/cutlery.svg.192dpi.png")),
                    Priority = 1,
                    ConfirmationPopup = true
                };
                args.Verbs.Add(verbDigest);
            }
            else if (vored.Digesting)
            {
                InnateVerb verbStopDigest = new()
                {
                    Act = () => StopDigest(prey),
                    Text = Loc.GetString("vore-stop-digest", ("entity", prey)),
                    Category = VerbCategory.Vore,
                    Priority = 1,
                };
                args.Verbs.Add(verbStopDigest);
            }
        }
    }

    public virtual void TryDevour(EntityUid uid, EntityUid target, VoreComponent? component = null) { }

    public virtual void Digest(EntityUid uid, VoredComponent? component = null) { }
    public virtual void StopDigest(EntityUid uid, VoredComponent? component = null) { }
    public virtual void ReleasePrey(EntityUid uid, VoredComponent? compnent = null) { }
}
