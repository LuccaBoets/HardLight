using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.NPC.HTN;
using Content.Server.Roles;
using Content.Server.Speech.Components;
using Content.Server.Temperature.Components;
using Content.Server.Zombies;
using Content.Shared._HL.Body.Components;
using Content.Shared.Administration;
using Content.Shared.CombatMode;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Mind;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Prying.Components;
using Content.Shared.Roles;
using Content.Shared.Tag;
using Content.Shared.Traits.Assorted;
using Content.Shared.Weapons.Melee;
using Content.Shared.Zombies;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.Commands;

/// <summary>
///     Admin command to fully reverse the zombie transformation on a target player.
///     Restores appearance, body systems, hands, factions, damage modifiers, and strips
///     all zombie-related components — as close to pre-zombification state as possible.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class DzlolCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    public override string Command => "dzlol";
    public override string Description => "Fully reverses zombie transformation on the target player.";
    public override string Help => "dzlol <username>";

    private static readonly ProtoId<TagPrototype> InvalidForGlobalSpawnSpellTag = "InvalidForGlobalSpawnSpell";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var names = _playerManager.Sessions.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            return CompletionResult.FromHintOptions(names, LocalizationManager.GetString("shell-argument-username-hint"));
        }

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(LocalizationManager.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!_playerManager.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError(LocalizationManager.GetString("shell-target-player-does-not-exist"));
            return;
        }

        var target = session.AttachedEntity;
        if (target == null)
        {
            shell.WriteError("Target player has no attached entity.");
            return;
        }

        var uid = target.Value;

        // --- Appearance & blood (requires ZombieComponent data, so do this first) ---
        var zombieSystem = _entities.System<ZombieSystem>();
        if (_entities.TryGetComponent<ZombieComponent>(uid, out var zombieComp))
        {
            zombieSystem.UnZombify(uid, uid, zombieComp);
            _entities.RemoveComponent<ZombieComponent>(uid);
        }

        // --- Zombie infection components ---
        _entities.RemoveComponent<PendingZombieComponent>(uid);
        _entities.RemoveComponent<ZombifyOnDeathComponent>(uid);
        _entities.RemoveComponent<IncurableZombieComponent>(uid);

        // --- Voice accent added during zombification ---
        _entities.RemoveComponent<ReplacementAccentComponent>(uid);

        // --- Zombie-specific emotes ---
        var autoEmote = _entities.System<AutoEmoteSystem>();
        var emoteOnDamage = _entities.System<EmoteOnDamageSystem>();
        autoEmote.RemoveEmote(uid, "ZombieGroan");
        emoteOnDamage.RemoveEmote(uid, "Scream");

        // --- Prying ability (only added to humanoid zombies) ---
        _entities.RemoveComponent<PryingComponent>(uid);

        // --- NPC AI (added to let unminded zombies function) ---
        _entities.RemoveComponent<HTNComponent>(uid);

        // --- Ghost role takeout (added when zombified entity has no mind) ---
        _entities.RemoveComponent<GhostRoleComponent>(uid);
        _entities.RemoveComponent<GhostTakeoverAvailableComponent>(uid);

        // --- Zombie antag role from the player's mind ---
        var mindSystem = _entities.System<MindSystem>();
        var roleSystem = _entities.System<SharedRoleSystem>();
        if (mindSystem.TryGetMind(uid, out var mindId, out _))
            roleSystem.MindTryRemoveRole<ZombieRoleComponent>(mindId);

        // --- Faction: clear Zombie, restore entity-prototype factions ---
        var factionSystem = _entities.System<NpcFactionSystem>();
        factionSystem.ClearFactions(uid);
        var meta = _entities.GetComponent<MetaDataComponent>(uid);
        var proto = meta.EntityPrototype;

        RestorePrototypeComponent(uid, proto, "ReplacementAccent");

        // Components stripped during zombification should come back with prototype values.
        RestorePrototypeComponent(uid, proto, "Respirator");
        RestorePrototypeComponent(uid, proto, "Barotrauma");
        RestorePrototypeComponent(uid, proto, "Hunger");
        RestorePrototypeComponent(uid, proto, "Thirst");
        RestorePrototypeComponent(uid, proto, "Reproductive");
        RestorePrototypeComponent(uid, proto, "ReproductivePartner");
        RestorePrototypeComponent(uid, proto, "LegsParalyzed");
        RestorePrototypeComponent(uid, proto, "ComplexInteraction");
        RestorePrototypeComponent(uid, proto, "Puller");
        RestorePrototypeComponent(uid, proto, "Pacified");
        RestorePrototypeComponent(uid, proto, "Prying");
        if (proto?.Components.TryGetValue("NpcFactionMember", out var factionEntry) == true
            && factionEntry.Component is NpcFactionMemberComponent protoFaction
            && protoFaction.Factions.Count > 0)
        {
            factionSystem.AddFactions(uid, protoFaction.Factions);
        }
        else
        {
            // Default crew faction for humanoid players when prototype doesn't define one.
            factionSystem.AddFaction(uid, "NanoTrasen");
        }

        // --- Damage modifier set (was forced to "Zombie") ---
        var damageableSystem = _entities.System<DamageableSystem>();
        if (proto?.Components.TryGetValue("Damageable", out var damageableEntry) == true
            && damageableEntry.Component is DamageableComponent protoDamageable)
        {
            damageableSystem.SetDamageModifierSetId(uid, protoDamageable.DamageModifierSetId);
        }
        else
        {
            damageableSystem.SetDamageModifierSetId(uid, null);
        }

        // --- Blood loss threshold (was clamped to 0) ---
        var bloodstreamSystem = _entities.System<BloodstreamSystem>();
        if (proto?.Components.TryGetValue("Bloodstream", out var bloodstreamEntry) == true
            && bloodstreamEntry.Component is BloodstreamComponent protoBloodstream)
        {
            bloodstreamSystem.SetBloodLossThreshold(uid, protoBloodstream.BloodlossThreshold);
        }
        else
        {
            bloodstreamSystem.SetBloodLossThreshold(uid, 0.9f);
        }

        // --- Melee weapon component (was mutated and sometimes added) ---
        if (proto?.Components.TryGetValue("MeleeWeapon", out var meleeEntry) == true)
            ((EntityManager) _entities).AddComponent(uid, meleeEntry, overwrite: true);
        else
            _entities.RemoveComponent<MeleeWeaponComponent>(uid);

        // --- Restore combat mode defaults (CanDisarm was disabled for zombies) ---
        var combatSystem = _entities.System<SharedCombatModeSystem>();
        if (_entities.TryGetComponent<CombatModeComponent>(uid, out var combatComp))
            combatSystem.SetCanDisarm(uid, true, combatComp);

        if (proto != null)
        {
            // Restore temperature cold-damage resistance clamped to 0 by zombification.
            if (proto.Components.TryGetValue("Temperature", out var tempEntry)
                && tempEntry.Component is TemperatureComponent protoTemp
                && _entities.TryGetComponent<TemperatureComponent>(uid, out var tempComp))
            {
                tempComp.ColdDamage = protoTemp.ColdDamage;
                _entities.Dirty(uid, tempComp);
            }

            // Restore HandsComponent (was fully removed during zombification).
            if (!_entities.HasComponent<HandsComponent>(uid)
                && proto.Components.TryGetValue("Hands", out var handsEntry)
                && handsEntry.Component is HandsComponent protoHands)
            {
                var handsSystem = _entities.System<SharedHandsSystem>();
                var handsComp = _entities.EnsureComponent<HandsComponent>(uid);
                foreach (var (handName, hand) in protoHands.Hands)
                {
                    handsSystem.AddHand(uid, handName, hand.Location, handsComp);
                }
            }
        }

        // --- Name modifier (removes "Zombie ___" prefix) ---
        _entities.System<NameModifierSystem>().RefreshNameModifiers(uid);

        // --- Movement speed (zombie had a debuff) ---
        _entities.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(uid);

        // --- Tag that blocked global spawn spell ---
        _entities.System<TagSystem>().RemoveTag(uid, InvalidForGlobalSpawnSpellTag);

        shell.WriteLine($"Fully reversed zombie state on {args[0]}.");
    }

    private void RestorePrototypeComponent(EntityUid uid, EntityPrototype? proto, string componentName)
    {
        if (proto?.Components.TryGetValue(componentName, out var entry) == true)
            ((EntityManager) _entities).AddComponent(uid, entry, overwrite: true);
    }
}
