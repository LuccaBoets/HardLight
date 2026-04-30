using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
///     Sent by an admin client to ask the server which ship would be targeted
///     by Save Ship for the given owner. Allows a confirmation dialog that
///     names the exact ship before saving begins.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestSaveShipPreviewMessage : EntityEventArgs
{
    public string OwnerUserId { get; }

    public RequestSaveShipPreviewMessage(string ownerUserId)
    {
        OwnerUserId = ownerUserId;
    }
}

[Serializable, NetSerializable]
public sealed class SaveShipPreviewResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public bool Success { get; }
    public string? Error { get; }
    public string? ShipName { get; }

    public SaveShipPreviewResponseMessage(string ownerUserId, bool success, string? error = null, string? shipName = null)
    {
        OwnerUserId = ownerUserId;
        Success = success;
        Error = error;
        ShipName = shipName;
    }
}

/// <summary>
///     Sent by an admin client to:
///       1. Teleport every player currently aboard the owner's ship to the
///          medbay rescue (fulton) beacon on a station.
///       2. Force-save the ship to the owner's client, delivering the file as
///          if they had used a save console.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestSaveShipMessage : EntityEventArgs
{
    public string OwnerUserId { get; }

    public RequestSaveShipMessage(string ownerUserId)
    {
        OwnerUserId = ownerUserId;
    }
}

[Serializable, NetSerializable]
public sealed class SaveShipResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public bool Success { get; }
    public string? Error { get; }
    public string? ShipName { get; }

    /// <summary>Number of players that were evicted from the ship to the medbay rescue beacon.</summary>
    public int EvictedCount { get; }

    public SaveShipResponseMessage(
        string ownerUserId,
        bool success,
        string? error = null,
        string? shipName = null,
        int evictedCount = 0)
    {
        OwnerUserId = ownerUserId;
        Success = success;
        Error = error;
        ShipName = shipName;
        EvictedCount = evictedCount;
    }
}
