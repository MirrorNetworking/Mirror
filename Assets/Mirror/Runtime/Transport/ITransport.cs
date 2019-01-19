// abstract transport layer component
// note: not all transports need a port, so add it to yours if needed.
using System;
using UnityEngine;

namespace Mirror
{
    // note: 'address' is ip / websocket url / ...
    public enum TransportEvent { Connected, Data, Disconnected }

    public interface ITransport
    {
        // client
        event Action ClientConnected;
        event Action<byte[]> ClientDataReceived;
        event Action<Exception> ClientError;
        event Action ClientDisconnected;

        bool IsClientConnected();
        void ClientConnect(string address);
        bool ClientSend(int channelId, byte[] data);
        void ClientDisconnect();
        // pause message handling while a scene load is in progress
        //
        // problem:
        //   if we handle packets (calling the msgDelegates) while a
        //   scene load is in progress, then all the handled data and state
        //   will be lost as soon as the scene load is finished, causing
        //   state bugs.
        //
        // solution:
        //   don't handle messages until scene load is finished. the
        //   transport layer will queue it automatically.
        void Pause();
        void Resume();


        // server
        bool IsServerActive();
        void ServerStart();
        bool ServerSend(int connectionId, int channelId, byte[] data);
        bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data);
        bool ServerDisconnect(int connectionId);
        bool GetConnectionInfo(int connectionId, out string address);
        void ServerStop();

        // common
        void Shutdown();
        int GetMaxPacketSize(int channelId=Channels.DefaultReliable);
    }
}