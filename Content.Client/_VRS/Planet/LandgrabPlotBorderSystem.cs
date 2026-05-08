using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;

namespace Content.Client._VRS.Planet;

/// <summary>
/// Manages the lifetime of <see cref="LandgrabPlotBorderOverlay"/>.
/// The overlay is added when a local player session exists and removed on detach.
/// </summary>
public sealed class LandgrabPlotBorderSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private LandgrabPlotBorderOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        if (_overlays.HasOverlay<LandgrabPlotBorderOverlay>())
            return;

        _overlay = new LandgrabPlotBorderOverlay();
        _overlays.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (_overlay != null)
        {
            _overlays.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }
}
