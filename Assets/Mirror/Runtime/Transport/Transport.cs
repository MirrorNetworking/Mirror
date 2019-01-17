// abstract transport layer component
using UnityEngine;

namespace Mirror
{
    // note: 'address' is ip / websocket url / ...
    public enum TransportEvent { Connected, Data, Disconnected }

    public abstract class Transport : MonoBehaviour
    {
        // client
        public abstract bool ClientConnected();
        public abstract void ClientConnect(string address, ushort port);
        public abstract bool ClientSend(int channelId, byte[] data);
        public abstract bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data);
        public abstract void ClientDisconnect();

        // server
        public abstract bool ServerActive();
        public abstract void ServerStart(string address, ushort port);
        public abstract bool ServerSend(int connectionId, int channelId, byte[] data);
        public abstract bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data);
        public abstract bool ServerDisconnect(int connectionId);
        public abstract bool GetConnectionInfo(int connectionId, out string address);
        public abstract void ServerStop();

        // common
        public abstract void Shutdown();
        public abstract int GetMaxPacketSize(int channelId=Channels.DefaultReliable);

        // When pressing Stop in the Editor, Unity keeps threads alive until we
        // press Start again (which might be a Unity bug).
        // Either way, we should disconnect client & server in OnApplicationQuit
        // so they don't keep running until we press Play again.
        // (this is not a problem in builds)
        //
        // virtual so that inheriting classes' OnApplicationQuit() can call base.OnApplicationQuit() too
        public virtual void OnApplicationQuit()
        {
            Shutdown();
        }
    }
}