using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
///     Sent by an admin client to ask the server to FTL-jump a player's ship to
///     a clear point near its current position. Used to "unstick" ships that
///     have ended up overlapping (knotted) with another grid.
///     Server replies with <see cref="UnstickPlayerShipResponseMessage"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestUnstickPlayerShipMessage : EntityEventArgs
{
    /// <summary>
    ///     The player whose ship should be unstuck. Stringified <c>NetUserId</c>,
    ///     matching <c>ShuttleDeedComponent.OwnerUserId</c>.
    /// </summary>
    public string OwnerUserId { get; }

    public RequestUnstickPlayerShipMessage(string ownerUserId)
    {
        OwnerUserId = ownerUserId;
    }
}

/// <summary>
///     Sent by an admin client to ask the server which ship would be targeted
///     by unstick for the given owner. This allows a confirmation dialog that
///     names the exact ship before FTL is triggered.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestUnstickPlayerShipPreviewMessage : EntityEventArgs
{
    public string OwnerUserId { get; }

    public RequestUnstickPlayerShipPreviewMessage(string ownerUserId)
    {
        OwnerUserId = ownerUserId;
    }
}

[Serializable, NetSerializable]
public sealed class UnstickPlayerShipPreviewResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public bool Success { get; }
    public string? Error { get; }
    public string? ShipName { get; }

    public UnstickPlayerShipPreviewResponseMessage(string ownerUserId, bool success, string? error = null, string? shipName = null)
    {
        OwnerUserId = ownerUserId;
        Success = success;
        Error = error;
        ShipName = shipName;
    }
}

[Serializable, NetSerializable]
public sealed class UnstickPlayerShipResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public bool Success { get; }

    /// <summary>
    ///     Set when the request was rejected or no clear spot could be found.
    /// </summary>
    public string? Error { get; }

    /// <summary>Display name of the ship that was nudged (when <see cref="Success"/>).</summary>
    public string? ShipName { get; }

    /// <summary>Target world position the ship was sent to (when <see cref="Success"/>).</summary>
    public Vector2 NewPosition { get; }

    public UnstickPlayerShipResponseMessage(string ownerUserId, bool success, string? error = null, string? shipName = null, Vector2 newPosition = default)
    {
        OwnerUserId = ownerUserId;
        Success = success;
        Error = error;
        ShipName = shipName;
        NewPosition = newPosition;
    }
}
