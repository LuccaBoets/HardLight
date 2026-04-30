using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
///     Admin-side request for a player's current bank balance.
///     Reply: <see cref="PlayerBankInfoResponseMessage"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestPlayerBankInfoMessage : EntityEventArgs
{
    public string OwnerUserId { get; }

    public RequestPlayerBankInfoMessage(string ownerUserId)
    {
        OwnerUserId = ownerUserId;
    }
}

[Serializable, NetSerializable]
public sealed class PlayerBankInfoResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public int Balance { get; }
    public string? Error { get; }

    public PlayerBankInfoResponseMessage(string ownerUserId, int balance, string? error = null)
    {
        OwnerUserId = ownerUserId;
        Balance = balance;
        Error = error;
    }
}

/// <summary>
///     Admin-side request to modify a player's bank balance.
///     Reply: <see cref="ModifyPlayerBankResponseMessage"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestModifyPlayerBankMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public int Amount { get; }
    public string Reason { get; }

    public RequestModifyPlayerBankMessage(string ownerUserId, int amount, string reason)
    {
        OwnerUserId = ownerUserId;
        Amount = amount;
        Reason = reason;
    }
}

[Serializable, NetSerializable]
public sealed class ModifyPlayerBankResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public int NewBalance { get; }
    public string? Error { get; }

    public ModifyPlayerBankResponseMessage(string ownerUserId, int newBalance, string? error = null)
    {
        OwnerUserId = ownerUserId;
        NewBalance = newBalance;
        Error = error;
    }
}
