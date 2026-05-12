using Content.Shared._Starlight.NullSpace;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._Starlight.NullSpace;

public sealed class BluespaceFlasherRadiusOverlay : global::Robust.Client.Graphics.Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private readonly TransformSystem _xform;

    private static readonly Color FillColor = new(0.2f, 0.5f, 1f, 0.08f);
    private static readonly Color BorderColor = new(0.3f, 0.6f, 1f, 0.55f);

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public BluespaceFlasherRadiusOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xform = _entityManager.System<TransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var mapId = args.MapId;

        var query = _entityManager.EntityQueryEnumerator<BluespaceFlasherVisualsComponent, TransformComponent>();
        while (query.MoveNext(out _, out var visuals, out var xform))
        {
            if (xform.MapID != mapId || !xform.Anchored)
                continue;

            var worldPos = _xform.GetWorldPosition(xform);
            handle.DrawCircle(worldPos, visuals.Radius, FillColor, filled: true);
            handle.DrawCircle(worldPos, visuals.Radius, BorderColor, filled: false);
        }
    }
}
