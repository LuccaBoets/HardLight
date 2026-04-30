#nullable enable
using System;
using System.Collections.Generic;
using Content.Shared.Administration;

namespace Content.Client.Administration.UI.Bwoink;

/// <summary>
///     Client-side mirror of <c>BwoinkSystem.ClassifyAHelpCategory</c>.
///     Pure function that runs on the latest player message text against the
///     already-synced <see cref="AhelpAdminConfigState"/> keyword lists.
///     No allocations on the hot path beyond a single <see cref="string.ToLowerInvariant"/>.
///     Cost is O(categories * keywords) per call, only invoked when the admin
///     receives a player message — never on tick.
/// </summary>
public static class AhelpCategoryClassifier
{
    private static readonly char[] KeywordSeparators = { ',', ';', '\n' };

    /// <summary>
    ///     Returns the best-matching category name for <paramref name="message"/> using
    ///     keyword scoring identical to the server. Multi-word keywords score 2; single-word score 1.
    ///     Returns <c>null</c> if there is no config, no triage-enabled categories, or no match.
    /// </summary>
    public static string? Classify(string? message, AhelpAdminConfigState? config)
    {
        if (config is null || string.IsNullOrWhiteSpace(message))
            return null;

        var normalized = message.ToLowerInvariant();
        var bestScore = 0;
        string? bestCategory = null;

        foreach (var category in config.Categories)
        {
            // Only consider categories that have triage data; do not require triage to be
            // currently enabled — the goal is to inform the admin even if auto-triage is off.
            if (!category.HasTriage || string.IsNullOrWhiteSpace(category.Keywords))
                continue;

            var score = ScoreKeywords(normalized, category.Keywords);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestCategory = category.Name;
        }

        return bestScore > 0 ? bestCategory : null;
    }

    private static int ScoreKeywords(string normalizedMessage, string keywordList)
    {
        var score = 0;
        // Note: we deliberately avoid System.StringComparer here — the client
        // sandbox does not allow access to that type. Keywords are lowered
        // inline so a plain HashSet<string> with default ordinal comparison works.
        var seen = new HashSet<string>();

        foreach (var raw in keywordList.Split(KeywordSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var keyword = raw.Trim().ToLowerInvariant();
            if (keyword.Length == 0)
                continue;

            if (!seen.Add(keyword))
                continue;

            if (!normalizedMessage.Contains(keyword))
                continue;

            score += keyword.Contains(' ') ? 2 : 1;
        }

        return score;
    }
}
