using Content.Shared.Actions;

namespace Content.Shared.Shadowkin;

public sealed partial class DarkSwapActionEvent : InstantActionEvent
{
    [DataField]
    public float ManaCost = 1.0f;
}
