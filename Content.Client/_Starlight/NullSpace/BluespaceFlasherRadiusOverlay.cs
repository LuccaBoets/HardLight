using System.Numerics;
using Content.Shared._Starlight.NullSpace;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.NullSpace;

public sealed class BluespaceFlasherRadiusOverlay : global::Robust.Client.Graphics.Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly TransformSystem _xform;

    private Texture[]? _frames;
    private float[]? _frameDelays;
    private int _frameIndex;
    private float _frameTimer;

    private static readonly ResPath DomeRsiPath = new("/Textures/Effects/EnergyDome/energydome_big.rsi");
    private const string DomeStateName = "nullphase";
    private static readonly Color DomeColor = Color.FromHex("#6c15ae");
    private const float DomeVisualRadius = 15.0f;

    private static readonly Color FillColor = new(0.2f, 0.5f, 1f, 0.08f);
    private static readonly Color BorderColor = new(0.3f, 0.6f, 1f, 0.55f);

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public BluespaceFlasherRadiusOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xform = _entityManager.System<TransformSystem>();

        var rsi = _resourceCache.GetResource<RSIResource>(DomeRsiPath).RSI;
        if (rsi.TryGetState(DomeStateName, out var state))
        {
            _frames = state.GetFrames(RsiDirection.South);
            _frameDelays = state.GetDelays();
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (_frameDelays == null || _frameDelays.Length == 0)
            return;

        _frameTimer += args.DeltaSeconds;
        while (_frameTimer >= _frameDelays[_frameIndex])
        {
            _frameTimer -= _frameDelays[_frameIndex];
            _frameIndex = (_frameIndex + 1) % _frameDelays.Length;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var mapId = args.MapId;

        var query = _entityManager.AllEntityQueryEnumerator<BluespaceFlasherVisualsComponent, TransformComponent>();
        while (query.MoveNext(out _, out var visuals, out var xform))
        {
            if (xform.MapID != mapId || !xform.Anchored)
                continue;

            var worldPos = _xform.GetWorldPosition(xform);

            // Sprite dome
            if (_frames != null && _frames.Length > 0)
            {
                var size = DomeVisualRadius * 2f;
                handle.DrawTextureRect(_frames[_frameIndex], Box2.CenteredAround(worldPos, new Vector2(size, size)), DomeColor);
            }

            // Uncomment to debug the exact effect radius boundary
            // handle.DrawCircle(worldPos, visuals.Radius, FillColor, filled: true);
            // handle.DrawCircle(worldPos, visuals.Radius, BorderColor, filled: false);
        }
    }
}
