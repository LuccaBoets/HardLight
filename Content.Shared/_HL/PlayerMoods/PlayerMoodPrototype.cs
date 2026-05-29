using System.Linq;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Shared._HL.PlayerMoods;

/// <summary>
/// Defines a mood for the player mood trait system.
/// Use <c>type: mood</c> in YAML. This is distinct from thavenMood
/// and belongs entirely to the _HL player mood system.
/// </summary>
[Prototype("mood")]
[Serializable, NetSerializable]
public sealed partial class PlayerMoodPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string MoodName = string.Empty;

    [DataField(required: true)]
    public string MoodDesc = string.Empty;

    /// <summary>Other mood IDs that cannot coexist with this one.</summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdHashSetSerializer<PlayerMoodPrototype>))]
    public HashSet<string> Conflicts = new();

    /// <summary>
    /// Maps FTL variable name → dataset prototype ID.
    /// A random value is drawn from each dataset when this mood is rolled
    /// and substituted into the localized description.
    /// </summary>
    [DataField("moodVars", customTypeSerializer: typeof(PrototypeIdValueDictionarySerializer<string, DatasetPrototype>))]
    public Dictionary<string, string> MoodVarDatasets = new();

    [DataField]
    public bool AllowDuplicateMoodVars = false;

    /// <summary>
    /// Consent toggle ID (see consent.yml) that must be enabled by the player
    /// for this mood to be eligible during rolling. Leave null for no restriction.
    /// </summary>
    [DataField]
    public string? RequiredConsent = null;
}

/// <summary>
/// A runtime mood instance, created from a <see cref="PlayerMoodPrototype"/> with variables rolled.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayerMood
{
    public string ProtoId = string.Empty;
    public string MoodName = string.Empty;
    public string MoodDesc = string.Empty;
    public HashSet<string> Conflicts = new();
    public string? RequiredConsent = null;
    public Dictionary<string, string> MoodVars = new();

    public (string key, object val)[] GetLocArgs() =>
        MoodVars.Select(kv => (kv.Key, (object)kv.Value)).ToArray();

    public string GetLocName() => Loc.GetString(MoodName);

    public string GetLocDesc()
    {
        var args = GetLocArgs();
        return args.Length > 0
            ? Loc.GetString(MoodDesc, args)
            : Loc.GetString(MoodDesc);
    }
}
