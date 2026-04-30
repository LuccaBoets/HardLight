using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
///     Admin-side request for a one-shot snapshot of a player's current state
///     (session, body, mind, location, roles). Reply: <see cref="PlayerSnapshotResponseMessage"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestPlayerSnapshotMessage : EntityEventArgs
{
    public string OwnerUserId { get; }

    public RequestPlayerSnapshotMessage(string ownerUserId)
    {
        OwnerUserId = ownerUserId;
    }
}

[Serializable, NetSerializable]
public sealed class PlayerSnapshotResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public PlayerSnapshot? Snapshot { get; }
    public string? Error { get; }

    public PlayerSnapshotResponseMessage(string ownerUserId, PlayerSnapshot? snapshot, string? error = null)
    {
        OwnerUserId = ownerUserId;
        Snapshot = snapshot;
        Error = error;
    }
}

[Serializable, NetSerializable]
public sealed class PlayerSnapshot
{
    public bool Online { get; }
    public bool HasMind { get; }

    /// <summary>Entity the session is currently attached to, if any.</summary>
    public string AttachedEntityName { get; }

    /// <summary>Entity the mind originally owns (i.e. their actual body), if any.</summary>
    public string OwnedEntityName { get; }

    /// <summary>True iff <see cref="AttachedEntityName"/> differs from <see cref="OwnedEntityName"/> (ghost / VV / observer).</summary>
    public bool DetachedFromBody { get; }

    public Vector2 WorldPosition { get; }
    public string MapName { get; }

    public string[] Roles { get; }

    public PlayerSnapshot(
        bool online,
        bool hasMind,
        string attachedEntityName,
        string ownedEntityName,
        bool detachedFromBody,
        Vector2 worldPosition,
        string mapName,
        string[] roles)
    {
        Online = online;
        HasMind = hasMind;
        AttachedEntityName = attachedEntityName;
        OwnedEntityName = ownedEntityName;
        DetachedFromBody = detachedFromBody;
        WorldPosition = worldPosition;
        MapName = mapName;
        Roles = roles;
    }
}
