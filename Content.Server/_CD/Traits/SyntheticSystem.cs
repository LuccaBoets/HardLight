using Content.Server.Body.Systems;
using Content.Server.Database;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._CD.Traits;

public sealed class SyntheticSystem : EntitySystem // HardLight: Synth<Synthetic
{
    // Begin DeltaV - make strings static readonly
    private static readonly ProtoId<TypingIndicatorPrototype> RobotTypingIndicator = "robot";
    private static readonly ProtoId<ReagentPrototype> SyntheticBloodReagent = "SynthBlood";
    // End DeltaV

    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SyntheticComponent, ComponentStartup>(OnStartup); // HardLight: Synth<Synthetic
    }

    private void OnStartup(EntityUid uid, SyntheticComponent component, ComponentStartup args) // HardLight: Synth<Synthetic
    {
        if (TryComp<TypingIndicatorComponent>(uid, out var indicator))
        {
            indicator.TypingIndicatorPrototype = RobotTypingIndicator; // DeltaV - make strings static readonly
            Dirty(uid, indicator);
        }

        // Give them synthetic blood. Ion storm notif is handled in that system, // HardLight: synth<synthetic
        _bloodstream.ChangeBloodReagent(uid, SyntheticBloodReagent); // DeltaV - make strings static readonly, // HardLight: Synth<Synthetic
    }
}
