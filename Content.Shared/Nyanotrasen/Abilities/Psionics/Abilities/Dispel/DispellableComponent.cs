namespace Content.Shared.Abilities.Psionics
{
    [RegisterComponent]
    public sealed partial class DispellableComponent : Component
    {
        [DataField]
        public bool Disabled = false;
    }
}
