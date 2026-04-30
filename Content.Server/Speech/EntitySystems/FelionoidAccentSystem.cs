using Content.Server.Speech.Components;

namespace Content.Server.Speech.EntitySystems;

public sealed class FelionoidAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FelionoidAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, FelionoidAccentComponent component, AccentGetEvent args)
    {
        args.Message = _replacement.ApplyReplacements(args.Message, "cat");
    }
}
