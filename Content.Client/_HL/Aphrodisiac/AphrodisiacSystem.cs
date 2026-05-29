using Content.Shared._HL.Aphrodisiac;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._HL.Aphrodisiac;

public sealed class AphrodisiacSystem : SharedAphrodisiacSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private AphrodisiacOverlay _overlay = default!;
    private bool _showEffects;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AphrodisiacStatusEffectComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AphrodisiacStatusEffectComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<AphrodisiacStatusEffectComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<AphrodisiacStatusEffectComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new();

        _cfg.OnValueChanged(CCVars.ShowAphrodisiacEffects, OnShowEffectsChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCVars.ShowAphrodisiacEffects, OnShowEffectsChanged);
    }

    private void OnShowEffectsChanged(bool value)
    {
        _showEffects = value;

        if (!value)
        {
            _overlay.CurrentAphrodisiacPower = 0;
            _overlayMan.RemoveOverlay(_overlay);
            return;
        }

        if (_player.LocalEntity is { } local
            && HasComp<AphrodisiacStatusEffectComponent>(local)
            && !_overlayMan.HasOverlay<AphrodisiacOverlay>())
        {
            _overlayMan.AddOverlay(_overlay);
        }
    }

    private void OnInit(EntityUid uid, AphrodisiacStatusEffectComponent component, ComponentInit args)
    {
        if (!_showEffects)
            return;

        if (_player.LocalEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnShutdown(EntityUid uid, AphrodisiacStatusEffectComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity != uid)
            return;

        _overlay.CurrentAphrodisiacPower = 0;
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(EntityUid uid, AphrodisiacStatusEffectComponent component, LocalPlayerAttachedEvent args)
    {
        if (!_showEffects)
            return;

        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, AphrodisiacStatusEffectComponent component, LocalPlayerDetachedEvent args)
    {
        _overlay.CurrentAphrodisiacPower = 0;
        _overlayMan.RemoveOverlay(_overlay);
    }
}