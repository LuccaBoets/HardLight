namespace Content.Server._Mono.Cleanup;

/// <summary>
/// Bridges legacy CleanupProtectedGrid save data to the current CleanupImmune marker.
/// </summary>
public sealed class CleanupProtectedGridSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CleanupProtectedGridComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, CleanupProtectedGridComponent component, ComponentStartup args)
    {
        EnsureComp<CleanupImmuneComponent>(uid);
    }
}