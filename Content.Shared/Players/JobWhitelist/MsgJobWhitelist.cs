using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Players.JobWhitelist;

public sealed class MsgJobWhitelist : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public HashSet<string> Whitelist = new();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadVariableInt32();
        buffer.AssertAtLeastSize(count * NetMessageExt.MinSizeOfString); // HL: Patch GHSA-m25p-v44q-gjqq, make sure the buffer has as much data as the client sent.
        Whitelist.EnsureCapacity(count);

        for (var i = 0; i < count; i++)
        {
            Whitelist.Add(buffer.ReadString());
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Whitelist.Count);

        foreach (var ban in Whitelist)
        {
            buffer.Write(ban);
        }
    }
}
