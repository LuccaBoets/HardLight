using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
///     Sent by an admin client to teleport a player's attached entity to a
///     public station spawn point (preferably arrivals / latejoin).
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestTeleportPlayerToStationMessage : EntityEventArgs
{
    public string OwnerUserId { get; }

    public RequestTeleportPlayerToStationMessage(string ownerUserId)
    {
        OwnerUserId = ownerUserId;
    }
}

[Serializable, NetSerializable]
public sealed class TeleportPlayerToStationResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public bool Success { get; }
    public string? Error { get; }
    public string? DestinationName { get; }
    public Vector2 DestinationPosition { get; }

    public TeleportPlayerToStationResponseMessage(
        string ownerUserId,
        bool success,
        string? error = null,
        string? destinationName = null,
        Vector2 destinationPosition = default)
    {
        OwnerUserId = ownerUserId;
        Success = success;
        Error = error;
        DestinationName = destinationName;
        DestinationPosition = destinationPosition;
    }
}
