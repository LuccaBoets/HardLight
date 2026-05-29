using Robust.Client.Graphics;
using Robust.Shared.Player;
using Robust.Shared.Physics.Events;
using Content.Shared._Starlight.NullSpace;
using Robust.Shared.Prototypes;
using Content.Client._Starlight.Overlay;
using Content.Client._Starlight.NullSpace;

namespace Content.Client._Starlight;

public sealed partial class EtherealSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly ISharedPlayerManager _playerMan = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private static readonly ProtoId<ShaderPrototype> NullSpaceShaderId = "NullSpaceShader";

    private NullSpaceOverlay _overlay = default!;
    private BluespaceFlasherRadiusOverlay? _flasherOverlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullSpaceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NullSpaceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NullSpaceComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<NullSpaceComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<NullSpaceComponent, PreventCollideEvent>(PreventCollision);

        SubscribeLocalEvent<ShowNullSpaceComponent, ComponentInit>(OnShowInit);
        SubscribeLocalEvent<ShowNullSpaceComponent, ComponentShutdown>(OnShowShutdown);
        SubscribeLocalEvent<ShowNullSpaceComponent, LocalPlayerAttachedEvent>(OnShowPlayerAttached);
        SubscribeLocalEvent<ShowNullSpaceComponent, LocalPlayerDetachedEvent>(OnShowPlayerDetached);

        _overlay = new(_prototypeManager.Index(NullSpaceShaderId));
    }

    private void OnInit(EntityUid uid, NullSpaceComponent component, ComponentInit args)
    {
        if (uid != _playerMan.LocalEntity)
            return;

        _overlayMan.AddOverlay(_overlay);
        AddFlasherOverlay();
    }

    private void OnShutdown(EntityUid uid, NullSpaceComponent component, ComponentShutdown args)
    {
        if (uid != _playerMan.LocalEntity)
            return;

        _overlayMan.RemoveOverlay(_overlay);
        if (!HasComp<ShowNullSpaceComponent>(uid))
            RemoveFlasherOverlay();
    }

    private void OnPlayerAttached(EntityUid uid, NullSpaceComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
        AddFlasherOverlay();
    }

    private void OnPlayerDetached(EntityUid uid, NullSpaceComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
        if (!HasComp<ShowNullSpaceComponent>(uid))
            RemoveFlasherOverlay();
    }

    private void OnShowInit(EntityUid uid, ShowNullSpaceComponent component, ComponentInit args)
    {
        if (uid != _playerMan.LocalEntity)
            return;
        AddFlasherOverlay();
    }

    private void OnShowShutdown(EntityUid uid, ShowNullSpaceComponent component, ComponentShutdown args)
    {
        if (uid != _playerMan.LocalEntity)
            return;
        if (!HasComp<NullSpaceComponent>(uid))
            RemoveFlasherOverlay();
    }

    private void OnShowPlayerAttached(EntityUid uid, ShowNullSpaceComponent component, LocalPlayerAttachedEvent args)
    {
        AddFlasherOverlay();
    }

    private void OnShowPlayerDetached(EntityUid uid, ShowNullSpaceComponent component, LocalPlayerDetachedEvent args)
    {
        if (!HasComp<NullSpaceComponent>(uid))
            RemoveFlasherOverlay();
    }

    private void AddFlasherOverlay()
    {
        if (_overlayMan.HasOverlay<BluespaceFlasherRadiusOverlay>())
            return;
        _flasherOverlay = new BluespaceFlasherRadiusOverlay();
        _overlayMan.AddOverlay(_flasherOverlay);
    }

    private void RemoveFlasherOverlay()
    {
        _overlayMan.RemoveOverlay<BluespaceFlasherRadiusOverlay>();
        _flasherOverlay = null;
    }

    private void PreventCollision(EntityUid uid, NullSpaceComponent component, ref PreventCollideEvent args)
    {
        args.Cancelled = true;
    }
}
