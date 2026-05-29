namespace Content.Shared.Paint;

/// <summary>
/// Colors target and consumes reagent on each color success.
/// </summary>
public abstract class SharedPaintSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!; // HardLight

    public virtual void UpdateAppearance(EntityUid uid, PaintedComponent? component = null)
    {
        if (!Resolve(uid, ref component, false)) // HardLight
            return;

        _appearance.SetData(uid, PaintVisuals.Painted, component.Enabled); // HardLight
    }
}
