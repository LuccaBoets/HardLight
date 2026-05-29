using Content.Server._CD.Traits; // CD: Synthetic trait, // // HardLight: Synth<Synthetic
using Content.Server.Silicons.Laws;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Station.Components;
// CD start: Synthetic trait, // HardLight: Synth<Synthetic
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Robust.Shared.Player;
using Robust.Shared.Random;
// CD end

namespace Content.Server.StationEvents.Events;

public sealed class IonStormRule : StationEventSystem<IonStormRuleComponent>
{
    [Dependency] private readonly IonStormSystem _ionStorm = default!;
    [Dependency] private readonly IChatManager _chatManager = default!; // CD: Used for synthetic trait, // HardLight: synth<synthetic

    protected override void Started(EntityUid uid, IonStormRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        // Frontier: Affect all silicon beings in the sector, not just on-station.
        // if (!TryGetRandomStation(out var chosenStation))
        //     return;
        // Frontier end

        // CD: Go through everyone with the SyntheticComponent and inform them a storm is happening, // HardLight: Synth<Synthetic
        var syntheticQuery = EntityQueryEnumerator<SyntheticComponent>(); // HardLight: Synth<Synthetic
        while (syntheticQuery.MoveNext(out var ent, out var syntheticComp)) // HardLight: synth<synthetic
        {
            if (RobustRandom.Prob(syntheticComp.AlertChance)) // HardLight: synth<synthetic
                continue;

            if (!TryComp<ActorComponent>(ent, out var actor))
                continue;

            var msg = Loc.GetString("station-event-ion-storm-synthetic"); // HardLight: synth<synthetic
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
            _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, actor.PlayerSession.Channel, colorOverride: Color.Yellow);
        }
        // CD end: Synthetic trait, // HardLight: Synth<Synthetic

        var query = EntityQueryEnumerator<SiliconLawBoundComponent, TransformComponent, IonStormTargetComponent>();
        while (query.MoveNext(out var ent, out var lawBound, out var xform, out var target))
        {
            // Frontier: Affect all silicon beings in the sector, not just on-station.
            // // only affect law holders on the station
            // if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != chosenStation)
            //     continue;
            // Frontier end

            _ionStorm.IonStormTarget((ent, lawBound, target));
        }
    }
}
