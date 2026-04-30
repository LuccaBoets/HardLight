using Content.Server.Inventory;
using Content.Server.Radio.Components;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Silicons.Borgs;

/// <summary>
/// Server-side logic for borg type switching. Handles more heavyweight and server-specific switching logic.
/// </summary>
public sealed class BorgSwitchableTypeSystem : SharedBorgSwitchableTypeSystem
{
    [Dependency] private readonly BorgSystem _borgSystem = default!;
    [Dependency] private readonly ServerInventorySystem _inventorySystem = default!;

    protected override void SelectBorgModule(Entity<BorgSwitchableTypeComponent> ent, ProtoId<BorgTypePrototype> borgType)
    {
        var prototype = Prototypes.Index(borgType);

        // Assign radio channels.
        // HardLight: write to IntrinsicChannels (rather than just Channels) so per-borg-type
        // channels survive EncryptionChannelsChangedEvent rebuilds in IntrinsicRadioKeySystem.
        // Without this, installing or removing an encryption key would clobber the borg type's
        // own radio channels (e.g. SecBorg losing Security comms after adding any other key).
        string[] radioChannels = [.. ent.Comp.InherentRadioChannels, .. prototype.RadioChannels];
        var keyHolder = CompOrNull<EncryptionKeyHolderComponent>(ent);

        if (TryComp(ent, out IntrinsicRadioTransmitterComponent? transmitter))
        {
            transmitter.IntrinsicChannels = [.. radioChannels];
            transmitter.Channels = [.. radioChannels];
            if (keyHolder != null)
                transmitter.Channels.UnionWith(keyHolder.Channels);
        }

        if (TryComp(ent, out ActiveRadioComponent? activeRadio))
        {
            activeRadio.IntrinsicChannels = [.. radioChannels];
            activeRadio.Channels = [.. radioChannels];
            if (keyHolder != null)
                activeRadio.Channels.UnionWith(keyHolder.Channels);
        }

        // Borg transponder for the robotics console
        if (TryComp(ent, out BorgTransponderComponent? transponder))
        {
            _borgSystem.SetTransponderSprite(
                (ent.Owner, transponder),
                new SpriteSpecifier.Rsi(new ResPath(prototype.SpritePath), prototype.SpriteBodyState));

            _borgSystem.SetTransponderName(
                (ent.Owner, transponder),
                Loc.GetString($"borg-type-{borgType}-transponder"));
        }

        // Configure modules
        if (TryComp(ent, out BorgChassisComponent? chassis))
        {
            var chassisEnt = (ent.Owner, chassis);
            _borgSystem.SetMaxModules(
                chassisEnt,
                prototype.ExtraModuleCount + prototype.DefaultModules.Length);

            _borgSystem.SetModuleWhitelist(chassisEnt, prototype.ModuleWhitelist);

            foreach (var module in prototype.DefaultModules)
            {
                var moduleEntity = Spawn(module);
                var borgModule = Comp<BorgModuleComponent>(moduleEntity);
                _borgSystem.SetBorgModuleDefault((moduleEntity, borgModule), true);
                _borgSystem.InsertModule(chassisEnt, moduleEntity);
            }
        }

        // Configure special components
        if (Prototypes.TryIndex(ent.Comp.SelectedBorgType, out var previousPrototype))
        {
            if (previousPrototype.AddComponents is { } removeComponents)
                EntityManager.RemoveComponents(ent, removeComponents);
        }

        if (prototype.AddComponents is { } addComponents)
        {
            EntityManager.AddComponents(ent, addComponents);
        }

        // Configure inventory template (used for hat spacing)
        if (TryComp(ent, out InventoryComponent? inventory))
        {
            _inventorySystem.SetTemplateId((ent.Owner, inventory), prototype.InventoryTemplateId);
        }

        base.SelectBorgModule(ent, borgType);
    }
}
