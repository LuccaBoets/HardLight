using Robust.Shared.Player;
using Robust.Shared.Network;

namespace Content.Server.Voting.Managers;

/// <summary>
/// Immutable snapshot of a vote after it finishes or is cancelled.
/// </summary>
public sealed class VoteHistoryEntry
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string InitiatorText { get; init; } = string.Empty;
    public bool Cancelled { get; init; }
    public IReadOnlyList<string> OptionTexts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<VoteHistoryCastEntry> CastVotes { get; init; } = Array.Empty<VoteHistoryCastEntry>();
}

/// <summary>
/// Immutable record of one voter's final selection.
/// </summary>
public sealed class VoteHistoryCastEntry
{
    public string PlayerName { get; init; } = string.Empty;
    public NetUserId UserId { get; init; }
    public int OptionId { get; init; }
}
