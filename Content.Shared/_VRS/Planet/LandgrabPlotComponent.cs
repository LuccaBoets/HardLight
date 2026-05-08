using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._VRS.Planet;

/// <summary>
/// Placed on a plot grid that has been claimed via the Landgrab PDA app.
/// Identifies the grid as a player-owned outpost so it can be managed and
/// so overlap checks can prevent two plots being placed on top of each other.
/// </summary>
[RegisterComponent]
public sealed partial class LandgrabPlotComponent : Component
{
    /// <summary>
    /// Player ckey that owns this plot.
    /// </summary>
    [DataField]
    public string OwnerCKey = string.Empty;

    /// <summary>
    /// Display name of the owner (for inspect / overlay).
    /// </summary>
    [DataField]
    public string OwnerName = string.Empty;

    /// <summary>
    /// Half-extent of the plot in tiles (plot is PlotSize × PlotSize tiles centered on the grid origin).
    /// </summary>
    [DataField]
    public int PlotSize = 32;
}
