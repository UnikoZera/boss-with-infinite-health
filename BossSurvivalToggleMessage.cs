using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace BossSurvival;

public struct BossSurvivalToggleMessage : INetMessage
{
    public bool Enabled;
    public bool Request;

    public bool ShouldBroadcast => false;
    public NetTransferMode Mode => NetTransferMode.Reliable;
    public LogLevel LogLevel => LogLevel.VeryDebug;
    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteBool(Enabled);
        writer.WriteBool(Request);
    }

    public void Deserialize(PacketReader reader)
    {
        Enabled = reader.ReadBool();
        Request = reader.ReadBool();
    }
}
