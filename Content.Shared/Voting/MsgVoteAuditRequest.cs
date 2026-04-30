using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Voting;

/// <summary>
///     Client → Server: request vote audit data from the server.
///     If <see cref="WantInspect"/> is false, the server returns a recent-vote list.
///     If true, the server returns the full breakdown for <see cref="VoteId"/>.
/// </summary>
public sealed class MsgVoteAuditRequest : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public bool WantInspect;
    public int VoteId;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        WantInspect = buffer.ReadBoolean();
        buffer.ReadPadBits();
        if (WantInspect)
            VoteId = buffer.ReadVariableInt32();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(WantInspect);
        buffer.WritePadBits();
        if (WantInspect)
            buffer.WriteVariableInt32(VoteId);
    }
}
