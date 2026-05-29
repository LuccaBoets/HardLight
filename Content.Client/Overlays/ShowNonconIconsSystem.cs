using Content.Client._Common.Consent;
using Content.Shared._Common.CCVar;
using Content.Shared._Common.Consent;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed class ShowNonconIconsSystem : EntitySystem
{
    [Dependency] private readonly IClientConsentManager _consentManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly ProtoId<ConsentTogglePrototype> NonconConsentToggle = "NonconIcon";
    private static readonly ProtoId<ConsentTogglePrototype> NonconAggressorToggle = "NonconAggressor";
    private static readonly ProtoId<ConsentTogglePrototype> NonconVictimToggle = "NonconVictim";

    // Default palette (red / blue / purple). The "default" Aggressor/neither
    // case still uses the legacy NonconIcon prototype so existing users who
    // only have NonconIcon enabled (no role flag) see no visual change.
    private static readonly ProtoId<SecurityIconPrototype> NonconStatusIcon = "NonconIcon";
    private static readonly ProtoId<SecurityIconPrototype> NonconStatusIconVictim = "NonconIconVictim";
    private static readonly ProtoId<SecurityIconPrototype> NonconStatusIconEither = "NonconIconEither";

    // Colorblind-safe palette (amber / teal-green / lavender), selected via the
    // consent.noncon_colorblind_palette client CVar. Same shapes as default.
    private static readonly ProtoId<SecurityIconPrototype> NonconStatusIconAggressorCb = "NonconIconAggressorColorblind";
    private static readonly ProtoId<SecurityIconPrototype> NonconStatusIconVictimCb = "NonconIconVictimColorblind";
    private static readonly ProtoId<SecurityIconPrototype> NonconStatusIconEitherCb = "NonconIconEitherColorblind";

    private bool _colorblindPalette;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConsentComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);

        _cfg.OnValueChanged(ConsentSystemCCVars.ConsentNonconColorblindPalette,
            v => _colorblindPalette = v, true);
    }

    private void OnGetStatusIconsEvent(Entity<ConsentComponent> ent, ref GetStatusIconsEvent ev)
    {
        if (!_consentManager.HasLoaded)
            return;

        // Mutual opt-in: the local viewer must want to see the icon, and the target must want to display it.
        var viewerToggles = _consentManager.GetConsentSettings().Toggles;
        var targetToggles = ent.Comp.ConsentSettings.Toggles;

        if (!viewerToggles.ContainsKey(NonconConsentToggle) ||
            !targetToggles.ContainsKey(NonconConsentToggle))
        {
            return;
        }

        // Pick marker icon based on the TARGET's selected role(s):
        //   Aggressor only  -> red    (default) / amber       (colorblind)
        //   Victim only     -> blue   (default) / teal-green  (colorblind)
        //   Both            -> purple (default) / lavender    (colorblind)
        //   Neither         -> red    (default) / amber       (colorblind)
        // The "neither" fallback intentionally maps to the Aggressor variant so
        // existing players who only have NonconIcon set see no change in the
        // default palette, while colorblind viewers still get a distinguishable
        // marker (amber) for unflagged opted-in players.
        var aggressor = targetToggles.ContainsKey(NonconAggressorToggle);
        var victim = targetToggles.ContainsKey(NonconVictimToggle);

        ProtoId<SecurityIconPrototype> iconId;
        if (aggressor && victim)
            iconId = _colorblindPalette ? NonconStatusIconEitherCb : NonconStatusIconEither;
        else if (victim)
            iconId = _colorblindPalette ? NonconStatusIconVictimCb : NonconStatusIconVictim;
        else
            iconId = _colorblindPalette ? NonconStatusIconAggressorCb : NonconStatusIcon;

        if (_prototype.TryIndex<SecurityIconPrototype>(iconId, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }
}