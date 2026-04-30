using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Voting;

/// <summary>
///     Server → Client: response to a <see cref="MsgVoteAuditRequest"/>.
///     When <see cref="IsInspect"/> is false, <see cref="Votes"/> is populated (recent-vote list).
///     When true, the inspect fields are populated (full breakdown for one vote).
/// </summary>
public sealed class MsgVoteAuditResponse : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public bool IsInspect;

    // --- List mode ---
    public VoteAuditEntry[] Votes = Array.Empty<VoteAuditEntry>();

    // --- Inspect mode ---
    public int InspectId;
    public string InspectTitle = string.Empty;
    public string InspectInitiator = string.Empty;
    public string InspectStatus = string.Empty; // "ACTIVE", "FINISHED", or "CANCELLED"
    public VoteAuditOption[] Options = Array.Empty<VoteAuditOption>();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        IsInspect = buffer.ReadBoolean();
        buffer.ReadPadBits();

        if (!IsInspect)
        {
            var count = buffer.ReadVariableInt32();
            Votes = new VoteAuditEntry[count];
            for (var i = 0; i < count; i++)
            {
                Votes[i] = new VoteAuditEntry
                {
                    Id = buffer.ReadVariableInt32(),
                    Title = buffer.ReadString(),
                    Initiator = buffer.ReadString(),
                    Status = buffer.ReadString(),
                };
            }
        }
        else
        {
            InspectId = buffer.ReadVariableInt32();
            InspectTitle = buffer.ReadString();
            InspectInitiator = buffer.ReadString();
            InspectStatus = buffer.ReadString();
            var optCount = buffer.ReadByte();
            Options = new VoteAuditOption[optCount];
            for (var i = 0; i < optCount; i++)
            {
                var text = buffer.ReadString();
                var voterCount = buffer.ReadVariableInt32();
                var voters = new string[voterCount];
                for (var j = 0; j < voterCount; j++)
                    voters[j] = buffer.ReadString();
                Options[i] = new VoteAuditOption { Text = text, Voters = voters };
            }
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(IsInspect);
        buffer.WritePadBits();

        if (!IsInspect)
        {
            buffer.WriteVariableInt32(Votes.Length);
            foreach (var v in Votes)
            {
                buffer.WriteVariableInt32(v.Id);
                buffer.Write(v.Title);
                buffer.Write(v.Initiator);
                buffer.Write(v.Status);
            }
        }
        else
        {
            buffer.WriteVariableInt32(InspectId);
            buffer.Write(InspectTitle);
            buffer.Write(InspectInitiator);
            buffer.Write(InspectStatus);
            buffer.Write((byte) Math.Min(Options.Length, 255));
            foreach (var opt in Options)
            {
                buffer.Write(opt.Text);
                buffer.WriteVariableInt32(opt.Voters.Length);
                foreach (var voter in opt.Voters)
                    buffer.Write(voter);
            }
        }
    }
}

public sealed class VoteAuditEntry
{
    public int Id;
    public string Title = string.Empty;
    public string Initiator = string.Empty;
    public string Status = string.Empty;
}

public sealed class VoteAuditOption
{
    public string Text = string.Empty;
    public string[] Voters = Array.Empty<string>();
}
