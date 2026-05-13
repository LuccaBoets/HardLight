// VRS port: Einstein Engines Language framework (via Triad).

using System.Text;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Shared._EinsteinEngines.Language.Systems;

public abstract class SharedLanguageSystem : EntitySystem
{
    /// <summary>
    ///     The language used as a fallback in cases where an entity suddenly becomes a Language Speaker (e.g. the usage of make-sentient).
    /// </summary>
    [ValidatePrototypeId<LanguagePrototype>]
    public static readonly string FallbackLanguagePrototype = "TauCetiBasic";

    /// <summary>
    ///     The language whose speakers are assumed to understand and speak every language. Should never be added directly.
    /// </summary>
    [ValidatePrototypeId<LanguagePrototype>]
    public static readonly string UniversalPrototype = "Universal";

    /// <summary>
    ///     Language used for Xenoglossy, should have same effects as Universal but with different language prototype.
    /// </summary>
    [ValidatePrototypeId<LanguagePrototype>]
    public static readonly string PsychomanticPrototype = "Psychomantic";

    /// <summary>
    /// A cached instance of <see cref="PsychomanticPrototype"/>.
    /// </summary>
    public static LanguagePrototype Psychomantic { get; private set; } = default!;

    /// <summary>
    ///     A cached instance of <see cref="UniversalPrototype"/>
    /// </summary>
    public static LanguagePrototype Universal { get; private set; } = default!;

    [Dependency] protected readonly IPrototypeManager _prototype = default!;
    [Dependency] protected readonly SharedGameTicker _ticker = default!;

    public override void Initialize()
    {
        Universal = _prototype.Index<LanguagePrototype>(UniversalPrototype);
        Psychomantic = _prototype.Index<LanguagePrototype>(PsychomanticPrototype);
    }

    public LanguagePrototype? GetLanguagePrototype(ProtoId<LanguagePrototype> id)
    {
        _prototype.TryIndex(id, out var proto);
        return proto;
    }

    /// <summary>
    ///     Obfuscate a message using the given language.
    /// </summary>
    public string ObfuscateSpeech(string message, LanguagePrototype language)
    {
        var builder = new StringBuilder();
        language.Obfuscation.Obfuscate(builder, message, this);

        return builder.ToString();
    }

    /// <summary>
    ///     Generates a stable pseudo-random number in the range (min, max) (inclusively) for the given seed.
    /// </summary>
    internal int PseudoRandomNumber(int seed, int min, int max)
    {
        seed = seed ^ (_ticker.RoundId * 127);
        var random = seed * 1103515245 + 12345;
        return min + Math.Abs(random) % (max - min + 1);
    }
}
