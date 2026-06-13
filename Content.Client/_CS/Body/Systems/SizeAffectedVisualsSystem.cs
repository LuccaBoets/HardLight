using System.Collections.Generic;
using Content.Shared.Body.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;

namespace Content.Client.Body.Systems;

/// <summary>
/// DEPRECATED: This system has been replaced by the event-driven size modification system.
/// Size scaling is now handled through HeightAdjustSystem which modifies HumanoidAppearanceComponent.
/// This system was causing double-scaling issues where:
/// 1. Server applies scale via HeightAdjustSystem -> HumanoidAppearanceComponent.Height/Width
/// 2. This system ALSO applied scale via SpriteComponent.Scale
/// Result: Scale applied twice (e.g., 1.3 x 1.3 = 1.69, appearing nearly doubled)
/// 
/// DO NOT RE-ENABLE without removing HeightAdjustSystem's visual scaling logic.
/// </summary>
public sealed class SizeAffectedVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        
        // System disabled - size scaling now handled by HeightAdjustSystem via HumanoidAppearanceComponent
        Logger.Info("SizeAffectedVisualsSystem: DISABLED - scaling handled by HeightAdjustSystem");
    }
}
