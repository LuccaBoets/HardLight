using System.Diagnostics.CodeAnalysis;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Weather;

public abstract class SharedWeatherSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IPrototypeManager ProtoMan = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedRoofSystem _roof = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private EntityQuery<BlockWeatherComponent> _blockQuery;
    private EntityQuery<WeatherStatusEffectComponent> _weatherQuery;

    public static readonly TimeSpan StartupTime = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan ShutdownTime = TimeSpan.FromSeconds(15);

    public override void Initialize()
    {
        base.Initialize();

        _blockQuery = GetEntityQuery<BlockWeatherComponent>();
        _weatherQuery = GetEntityQuery<WeatherStatusEffectComponent>();
    }

    public bool CanWeatherAffect(Entity<MapGridComponent?, RoofComponent?> ent, TileRef tileRef)
    {
        if (tileRef.Tile.IsEmpty)
            return true;

        if (!Resolve(ent, ref ent.Comp1))
            return false;

        if (Resolve(ent, ref ent.Comp2, false) && _roof.IsRooved((ent, ent.Comp1, ent.Comp2), tileRef.GridIndices))
            return false;

        var tileDef = (ContentTileDefinition) _tileDefManager[tileRef.Tile.TypeId];

        if (!tileDef.MapAtmosphere)
            return false;

        var anchoredEntities = _mapSystem.GetAnchoredEntitiesEnumerator(ent, ent.Comp1, tileRef.GridIndices);

        while (anchoredEntities.MoveNext(out var anchored))
        {
            if (_blockQuery.HasComponent(anchored.Value))
                return false;
        }

        return true;
    }

    public float GetWeatherPercent(Entity<StatusEffectComponent> ent)
    {
        var elapsed = Timing.CurTime - ent.Comp.StartEffectTime;
        var duration = ent.Comp.Duration;
        var remaining = duration - elapsed;

        if (remaining < ShutdownTime)
            return (float) (remaining / ShutdownTime);
        if (elapsed < StartupTime)
            return (float) (elapsed / StartupTime);

        return 1f;
    }

    public bool TryAddWeather(MapId mapId, EntProtoId weatherProto, [NotNullWhen(true)] out EntityUid? weatherEnt, TimeSpan? duration = null)
    {
        weatherEnt = null;

        if (!_mapSystem.TryGetMap(mapId, out var mapUid))
            return false;

        return TryAddWeather(mapUid.Value, weatherProto, out weatherEnt, duration);
    }

    public bool TryAddWeather(EntityUid mapUid, EntProtoId weatherProto, [NotNullWhen(true)] out EntityUid? weatherEnt, TimeSpan? duration = null)
    {
        return _statusEffects.TrySetStatusEffectDuration(mapUid, weatherProto, out weatherEnt, duration);
    }

    public bool HasWeather(MapId mapId, EntProtoId weatherProto)
    {
        if (!_mapSystem.TryGetMap(mapId, out var mapUid))
            return false;

        return _statusEffects.TryGetStatusEffect(mapUid.Value, weatherProto, out _);
    }

    public bool TryRemoveWeather(MapId mapId, EntProtoId weatherProto)
    {
        if (!_mapSystem.TryGetMap(mapId, out var mapUid))
            return false;

        return TryRemoveWeather(mapUid.Value, weatherProto);
    }

    public bool TryRemoveWeather(EntityUid mapUid, EntProtoId weatherProto)
    {
        if (!_statusEffects.TryGetStatusEffect(mapUid, weatherProto, out var weatherEnt))
            return false;

        if (!_weatherQuery.HasComp(weatherEnt))
            return false;

        return _statusEffects.TrySetStatusEffectDuration(mapUid, weatherProto, ShutdownTime);
    }

    public bool TrySetWeather(MapId mapId, EntProtoId? weatherProto, out EntityUid? weatherEnt, TimeSpan? duration = null)
    {
        weatherEnt = null;
        if (!_mapSystem.TryGetMap(mapId, out var mapUid))
            return false;

        if (_statusEffects.TryEffectsWithComp<WeatherStatusEffectComponent>(mapUid.Value, out var effects))
        {
            foreach (var effect in effects)
            {
                var effectProto = Prototype(effect);
                if (effectProto == null)
                    continue;

                if (effectProto != weatherProto)
                    TryRemoveWeather(mapUid.Value, effectProto);
                else
                    weatherEnt = effect;
            }
        }

        if (weatherProto == null)
            return true;

        if (weatherEnt != null)
        {
            TryAddWeather(mapUid.Value, weatherProto.Value, out weatherEnt, duration);
            return true;
        }

        return TryAddWeather(mapUid.Value, weatherProto.Value, out weatherEnt, duration);
    }
}
