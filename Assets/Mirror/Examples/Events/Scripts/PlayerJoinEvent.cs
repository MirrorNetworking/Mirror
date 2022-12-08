using Mirror.Core.Events;

namespace Mirror.Examples.Events
{
    /// <summary>
    /// Called when the a player connects after initialization
    /// </summary>
    public class PlayerJoinEvent : NetworkEvent
    {

        public uint playerNetId;

        public override void Write(NetworkWriter writer)
        {
            base.Write(writer);
            writer.WriteUInt(playerNetId);
        }

        public override void Read(NetworkReader reader)
        {
            base.Read(reader);
            playerNetId = reader.ReadUInt();
        }

    }
}
