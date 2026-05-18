// VRS port: Einstein Engines Language framework (via Triad).

using System.Text;
using Content.Shared._EinsteinEngines.Language.Systems;

namespace Content.Shared._EinsteinEngines.Language;

[ImplicitDataDefinitionForInheritors]
public abstract partial class ObfuscationMethod
{
    /// <summary>
    ///     The fallback obfuscation method, replaces the message with the string "&lt;?&gt;".
    /// </summary>
    public static readonly ObfuscationMethod Default = new ReplacementObfuscation
    {
        Replacement = new List<string> { "<?>" }
    };

    /// <summary>
    ///     Obfuscates the provided message and writes the result into the provided StringBuilder.
    /// </summary>
    internal abstract void Obfuscate(StringBuilder builder, string message, SharedLanguageSystem context);

    /// <summary>
    ///     Obfuscates the provided message. This method should only be used for debugging purposes.
    /// </summary>
    public string Obfuscate(string message)
    {
        var builder = new StringBuilder();
        Obfuscate(builder, message, IoCManager.Resolve<EntitySystemManager>().GetEntitySystem<SharedLanguageSystem>());
        return builder.ToString();
    }
}

/// <summary>
///     The most primitive method of obfuscation - replaces the entire message with one random replacement phrase.
/// </summary>
public partial class ReplacementObfuscation : ObfuscationMethod
{
    /// <summary>
    ///     A list of replacement phrases used in the obfuscation process.
    /// </summary>
    [DataField(required: true)]
    public List<string> Replacement = [];

    internal override void Obfuscate(StringBuilder builder, string message, SharedLanguageSystem context)
    {
        var idx = context.PseudoRandomNumber(message.GetHashCode(), 0, Replacement.Count - 1);
        builder.Append(Replacement[idx]);
    }
}

/// <summary>
///     Obfuscates the provided message by replacing each word with a random number of syllables in the range (min, max),
///     preserving the original punctuation to a resonable extent.
/// </summary>
public sealed partial class SyllableObfuscation : ReplacementObfuscation
{
    [DataField]
    public int MinSyllables = 1;

    [DataField]
    public int MaxSyllables = 4;

    internal override void Obfuscate(StringBuilder builder, string message, SharedLanguageSystem context)
    {
        const char eof = (char) 0;

        var wordBeginIndex = 0;
        var hashCode = 0;

        for (var i = 0; i <= message.Length; i++)
        {
            var ch = i < message.Length ? char.ToLower(message[i]) : eof;
            var isWordEnd = char.IsWhiteSpace(ch) || IsPunctuation(ch) || ch == eof;

            if (!isWordEnd)
                hashCode = hashCode * 31 + ch;

            if (isWordEnd)
            {
                var wordLength = i - wordBeginIndex;
                if (wordLength > 0)
                {
                    var newWordLength = context.PseudoRandomNumber(hashCode, MinSyllables, MaxSyllables);

                    for (var j = 0; j < newWordLength; j++)
                    {
                        var index = context.PseudoRandomNumber(hashCode + j, 0, Replacement.Count - 1);
                        builder.Append(Replacement[index]);
                    }
                }

                hashCode = 0;
                wordBeginIndex = i + 1;
            }

            if (isWordEnd && ch != eof)
                builder.Append(ch);
        }
    }

    private static bool IsPunctuation(char ch)
    {
        return ch is '.' or '!' or '?' or ',' or ':';
    }
}

/// <summary>
///     Obfuscates each sentence in the message by concatenating a number of obfuscation phrases.
/// </summary>
public sealed partial class PhraseObfuscation : ReplacementObfuscation
{
    [DataField]
    public int MinPhrases = 1;

    [DataField]
    public int MaxPhrases = 4;

    [DataField]
    public string Separator = " ";

    [DataField]
    public float Proportion = 1f / 3;

    internal override void Obfuscate(StringBuilder builder, string message, SharedLanguageSystem context)
    {
        var sentenceBeginIndex = 0;
        var hashCode = 0;

        for (var i = 0; i < message.Length; i++)
        {
            var ch = char.ToLower(message[i]);
            if (!IsPunctuation(ch) && i != message.Length - 1)
            {
                hashCode = hashCode * 31 + ch;
                continue;
            }

            var length = i - sentenceBeginIndex;
            if (length > 0)
            {
                var newLength = (int) Math.Clamp(Math.Pow(length, Proportion) - 1, MinPhrases, MaxPhrases);

                for (var j = 0; j < newLength; j++)
                {
                    var phraseIdx = context.PseudoRandomNumber(hashCode + j, 0, Replacement.Count - 1);
                    var phrase = Replacement[phraseIdx];
                    builder.Append(phrase);
                    builder.Append(Separator);
                }
            }
            sentenceBeginIndex = i + 1;

            if (IsPunctuation(ch))
                builder.Append(ch).Append(' ');
        }
    }

    private static bool IsPunctuation(char ch)
    {
        return ch is '.' or '!' or '?';
    }
}
