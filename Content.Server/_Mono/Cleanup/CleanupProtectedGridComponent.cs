using Robust.Shared.GameObjects;

namespace Content.Server._Mono.Cleanup;

/// <summary>
/// Legacy marker kept so old saved grids using the old component name still deserialize.
/// </summary>
[RegisterComponent]
public sealed partial class CleanupProtectedGridComponent : Component
{
}