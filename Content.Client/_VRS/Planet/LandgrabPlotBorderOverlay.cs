using System.Numerics;
using Content.Shared._VRS.Planet;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._VRS.Planet;

/// <summary>
/// Draws a ghosted border in the game world around every visible landgrab plot.
/// The local player's own plot is drawn in bright white; other players' plots
/// are drawn in a muted teal so ownership is immediately obvious.
/// Only visible when the camera is on a <see cref="PlanetPlotRegistryComponent"/> map.
/// </summary>
public sealed class LandgrabPlotBorderOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private readonly SharedTransformSystem _xform;

    /// <summary>White used for the local player's own plot.</summary>
    private static readonly Color OwnPlotColor = new Color(1.0f, 1.0f, 1.0f, 0.55f);

    /// <summary>Muted teal used for all other players' plots.</summary>
    private static readonly Color OtherPlotColor = new Color(0.30f, 0.80f, 0.85f, 0.35f);

    public LandgrabPlotBorderOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xform = _entities.System<SharedTransformSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // Only draw when on a planet map (has the registry component on the map entity).
        var mapUid = _mapManager.GetMapEntityId(args.MapId);
        return _entities.TryGetComponent(mapUid, out PlanetPlotRegistryComponent? _);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localCKey = _player.LocalSession?.Name ?? string.Empty;

        var enumerator = _entities.AllEntityQueryEnumerator<LandgrabPlotComponent, TransformComponent>();
        while (enumerator.MoveNext(out _, out var plot, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var worldPos = _xform.GetWorldPosition(xform);
            var half = plot.PlotSize * 0.5f;

            var color = string.Equals(plot.OwnerCKey, localCKey, StringComparison.OrdinalIgnoreCase)
                ? OwnPlotColor
                : OtherPlotColor;

            // Draw the four border lines of the plot square.
            var tl = new Vector2(worldPos.X - half, worldPos.Y + half);
            var tr = new Vector2(worldPos.X + half, worldPos.Y + half);
            var br = new Vector2(worldPos.X + half, worldPos.Y - half);
            var bl = new Vector2(worldPos.X - half, worldPos.Y - half);

            args.WorldHandle.DrawLine(tl, tr, color);
            args.WorldHandle.DrawLine(tr, br, color);
            args.WorldHandle.DrawLine(br, bl, color);
            args.WorldHandle.DrawLine(bl, tl, color);
        }
    }
}
