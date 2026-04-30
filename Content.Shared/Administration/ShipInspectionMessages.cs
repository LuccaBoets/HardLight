using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
///     Sent by an admin client to request a summary of all ships owned by a player.
///     Server replies with <see cref="PlayerShipInspectionResponseMessage"/>.
///     This is a one-shot, on-demand lookup — no subscription state is established.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestPlayerShipInspectionMessage : EntityEventArgs
{
    /// <summary>
    ///     The player whose ships should be inspected. Identified by their UserId
    ///     stringified, matching the format stored on <c>ShuttleDeedComponent.OwnerUserId</c>.
    /// </summary>
    public string OwnerUserId { get; }

    public RequestPlayerShipInspectionMessage(string ownerUserId)
    {
        OwnerUserId = ownerUserId;
    }
}

[Serializable, NetSerializable]
public sealed class PlayerShipInspectionResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public PlayerShipSummary[] Ships { get; }

    /// <summary>
    ///     Set when the request was rejected (no permission, server error, etc.).
    ///     When non-empty, <see cref="Ships"/> will be empty.
    /// </summary>
    public string? Error { get; }

    public PlayerShipInspectionResponseMessage(string ownerUserId, PlayerShipSummary[] ships, string? error = null)
    {
        OwnerUserId = ownerUserId;
        Ships = ships;
        Error = error;
    }
}

[Serializable, NetSerializable]
public sealed class PlayerShipSummary
{
    public string Name { get; }
    public string Suffix { get; }

    /// <summary>NetEntity of the grid (the ship). Useful for quick console actions like follow.</summary>
    public NetEntity GridUid { get; }

    public Vector2 WorldPosition { get; }
    public string MapName { get; }

    /// <summary>True if the ship is currently in any FTL state other than Available.</summary>
    public bool InFtl { get; }
    public string FtlState { get; }

    public bool OwnerOnline { get; }
    public bool PurchasedWithVoucher { get; }

    public PlayerShipSummary(
        string name,
        string suffix,
        NetEntity gridUid,
        Vector2 worldPosition,
        string mapName,
        bool inFtl,
        string ftlState,
        bool ownerOnline,
        bool purchasedWithVoucher)
    {
        Name = name;
        Suffix = suffix;
        GridUid = gridUid;
        WorldPosition = worldPosition;
        MapName = mapName;
        InFtl = inFtl;
        FtlState = ftlState;
        OwnerOnline = ownerOnline;
        PurchasedWithVoucher = purchasedWithVoucher;
    }
}
