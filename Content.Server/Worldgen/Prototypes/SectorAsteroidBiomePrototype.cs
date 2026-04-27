using System.Linq;
using Content.Server.Worldgen.Tools;
using Content.Shared.Maps;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Server.Worldgen.Prototypes;

[Prototype("sectorAsteroidBiome")]
public sealed partial class SectorAsteroidBiomePrototype : IPrototype
{
    private Dictionary<string, EntitySpawnCollectionCache>? _caches;

    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField("floorTiles", required: true)]
    public List<string> FloorTiles = new();

    [DataField("entries", required: true,
        customTypeSerializer: typeof(PrototypeIdDictionarySerializer<List<EntitySpawnEntry>, ContentTileDefinition>))]
    private Dictionary<string, List<EntitySpawnEntry>> _entries = default!;

    public Dictionary<string, EntitySpawnCollectionCache> Caches
    {
        get
        {
            if (_caches == null)
            {
                _caches = _entries
                    .Select(pair => new KeyValuePair<string, EntitySpawnCollectionCache>(pair.Key, new EntitySpawnCollectionCache(pair.Value)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            }

            return _caches;
        }
    }
}