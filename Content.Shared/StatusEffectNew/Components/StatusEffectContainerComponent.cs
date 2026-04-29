using Robust.Shared.Containers;

namespace Content.Shared.StatusEffectNew.Components;

[RegisterComponent]
public sealed partial class StatusEffectContainerComponent : Component
{
    public const string ContainerId = "status-effects";

    [ViewVariables]
    public Container? ActiveStatusEffects;
}
