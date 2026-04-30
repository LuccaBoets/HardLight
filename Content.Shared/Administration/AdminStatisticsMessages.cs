using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

[Serializable, NetSerializable]
public sealed class RequestAdminStatisticsMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class AdminStatisticsRoleInfo
{
    public string RoleName { get; }
    public int TakenSlots { get; }
    public int? OpenSlots { get; }

    public AdminStatisticsRoleInfo(string roleName, int takenSlots, int? openSlots)
    {
        RoleName = roleName;
        TakenSlots = takenSlots;
        OpenSlots = openSlots;
    }
}

[Serializable, NetSerializable]
public sealed class AdminStatisticsSnapshot
{
    public int OnlinePlayers { get; }
    public int AveragePingMs { get; }
    public int MaxPingMs { get; }
    public int HighPingPlayers { get; }
    public int RoundId { get; }
    public string RunLevel { get; }
    public TimeSpan RoundDuration { get; }
    public TimeSpan ServerUptime { get; }
    public AdminStatisticsRoleInfo[] RoleSlots { get; }
    public string[] Antags { get; }

    public AdminStatisticsSnapshot(
        int onlinePlayers,
        int averagePingMs,
        int maxPingMs,
        int highPingPlayers,
        int roundId,
        string runLevel,
        TimeSpan roundDuration,
        TimeSpan serverUptime,
        AdminStatisticsRoleInfo[] roleSlots,
        string[] antags)
    {
        OnlinePlayers = onlinePlayers;
        AveragePingMs = averagePingMs;
        MaxPingMs = maxPingMs;
        HighPingPlayers = highPingPlayers;
        RoundId = roundId;
        RunLevel = runLevel;
        RoundDuration = roundDuration;
        ServerUptime = serverUptime;
        RoleSlots = roleSlots;
        Antags = antags;
    }
}

[Serializable, NetSerializable]
public sealed class AdminStatisticsResponseMessage : EntityEventArgs
{
    public AdminStatisticsSnapshot? Snapshot { get; }
    public string? Error { get; }

    public AdminStatisticsResponseMessage(AdminStatisticsSnapshot? snapshot, string? error = null)
    {
        Snapshot = snapshot;
        Error = error;
    }
}
