using System.Collections.Generic;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Procedural;
using Content.Shared.Salvage.Expeditions.Modifiers;
using Content.Shared.Shuttles.Components; // HardLight
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Salvage.Expeditions;

public sealed class ExpeditionDiskSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SalvageSystem _salvage = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    private static readonly Dictionary<string, int> DifficultyNumbers = new()
    {
        { "NFEasy", 1 },
        { "NFModerate", 2 },
        { "NFHazardous", 3 },
        { "NFExtreme", 4 },
        { "NFNightmare", 5 },
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<ExpeditionDiskComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ExpeditionDiskComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(EntityUid uid, ExpeditionDiskComponent component, MapInitEvent args)
    {
        if (component.Seed == 0)
        {
            component.Seed = _random.Next();
            component.MissionType = (SalvageMissionType)_random.NextByte((byte)SalvageMissionType.Max);
        }

        if (component.DifficultyNumber <= 0 && DifficultyNumbers.TryGetValue(component.Difficulty, out var number))
        {
            component.DifficultyNumber = number;
        }
    }

    public bool TryActivateFromConsole(EntityUid consoleUid, EntityUid diskUid, ExpeditionDiskComponent? component = null)
    {
        if (!Resolve(diskUid, ref component, false))
            return false;

        if (_timing.CurTime < component.CooldownEnd)
        {
            var remaining = component.CooldownEnd - _timing.CurTime;
            _popupSystem.PopupEntity(Loc.GetString("expedition-disk-cooldown", ("time", remaining.ToString("hh\\:mm\\:ss"))), consoleUid, PopupType.MediumCaution);
            return false;
        }

        var consoleXform = Transform(consoleUid);
        if (consoleXform.GridUid == null)
        {
            _popupSystem.PopupEntity(Loc.GetString("expedition-disk-no-grid"), consoleUid, PopupType.MediumCaution);
            return false;
        }

        // HardLight: Do not allow expedition starts while shuttle FTL is unavailable.
        if (TryComp<FTLComponent>(consoleXform.GridUid.Value, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("shuttle-console-in-ftl"), consoleUid, PopupType.MediumCaution); // TODO: A better message for this.
            return false;
        }

        component.CooldownEnd = _timing.CurTime + TimeSpan.FromHours(1);

        var missionParams = new SalvageMissionParams
        {
            Index = 0,
            Seed = component.Seed,
            Difficulty = component.Difficulty,
            MissionType = component.MissionType,
        };

        _salvage.SpawnMissionFromDisk(missionParams, consoleXform.GridUid.Value, consoleUid, diskUid);
        _popupSystem.PopupEntity(Loc.GetString("expedition-disk-primed"), consoleUid, PopupType.Medium);
        return true;
    }

    private void OnExamined(EntityUid uid, ExpeditionDiskComponent component, ExaminedEvent args)
    {
        if (!_prototypeManager.TryIndex<Content.Shared.Procedural.SalvageDifficultyPrototype>(component.Difficulty, out var difficultyProto))
            return;

        var mission = _salvage.GetMission(component.MissionType, difficultyProto, component.Seed);
        var biomeProto = _prototypeManager.Index<SalvageBiomeModPrototype>(mission.Biome);

        var planet = string.IsNullOrWhiteSpace(Loc.GetString(biomeProto.Description))
            ? Loc.GetString(biomeProto.ID)
            : Loc.GetString(biomeProto.Description);

        var objective = Loc.GetString($"salvage-expedition-type-{component.MissionType}");

        var difficultyNumber = component.DifficultyNumber;
        if (difficultyNumber <= 0 && DifficultyNumbers.TryGetValue(component.Difficulty, out var mapped))
            difficultyNumber = mapped;

        args.PushMarkup(Loc.GetString("expedition-disk-details",
            ("planet", planet),
            ("difficulty", difficultyNumber),
            ("objective", objective)));

        // Always derive enemy from the current mission so it matches the disk's actual seed/difficulty/type.
        try
        {
            var factionProto = _prototypeManager.Index<SalvageFactionPrototype>(mission.Faction);
            var factionId = factionProto.ID ?? mission.Faction;
            if (factionId.StartsWith("NF", StringComparison.OrdinalIgnoreCase))
                factionId = factionId.Substring(2);
            else if (factionId.StartsWith("HL", StringComparison.OrdinalIgnoreCase))
                factionId = factionId.Substring(2);
            args.PushMarkup($"Enemy: {factionId}");
        }
        catch
        {
            // If anything goes wrong resolving the faction, silently skip the field.
        }

        if (_timing.CurTime < component.CooldownEnd)
        {
            var remaining = component.CooldownEnd - _timing.CurTime;
            args.PushMarkup(Loc.GetString("expedition-disk-cooldown", ("time", remaining.ToString("hh\\:mm\\:ss"))));
        }
        else
        {
            args.PushMarkup(Loc.GetString("expedition-disk-ready"));
        }
    }
}
