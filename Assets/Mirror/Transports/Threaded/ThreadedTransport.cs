// threaded transport to handle all the magic.
// implementations are automatically elevated to the worker thread
// by simply overwriting all the thread functions
//
// note that ThreadLog.cs is required for Debug.Log from threads to work in builds.
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Mirror
{
    // buffered events for main thread
    enum MainEventType
    {
        OnConnect,
        OnSend,
        OnReceive,
        OnError,
        OnDisconnect,
    }

    // buffered events for worker thread
    enum ThreadEventType
    {
        DoListen,
        DoSend,
        DoDisconnect,
        DoShutdown
    }

    struct MainEvent
    {
        public MainEventType type;
        public object        param;

        // some events have value type parameters: connectionId, error.
        // store them explicitly to avoid boxing allocations to 'object param'.
        public int             connectionId;
        public TransportError? error;

        public MainEvent(
            MainEventType type,
            object param,
            int connectionId,
            TransportError? error = null)
        {
            this.type = type;
            this.connectionId = connectionId;
            this.error = error;
            this.param = param;
        }
    }

    struct ThreadEvent
    {
        public ThreadEventType type;
        public object          param;

        // some events have value type parameters: connectionId.
        // store them explicitly to avoid boxing allocations to 'object param'.
        public int? connectionId;

        public ThreadEvent(
            ThreadEventType type,
            object param,
            int? connectionId = null)
        {
            this.type = type;
            this.connectionId = connectionId;
            this.param = param;
        }
    }

    public class ThreadedTransport : Transport
    {
        public override bool Available()
        {
            throw new NotImplementedException();
        }
        public override bool ClientConnected()
        {
            throw new NotImplementedException();
        }
        public override void ClientConnect(string address)
        {
            throw new NotImplementedException();
        }
        public override void ClientSend(ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            throw new NotImplementedException();
        }
        public override void ClientDisconnect()
        {
            throw new NotImplementedException();
        }
        public override Uri ServerUri()
        {
            throw new NotImplementedException();
        }
        public override bool ServerActive()
        {
            throw new NotImplementedException();
        }
        public override void ServerStart()
        {
            throw new NotImplementedException();
        }
        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
        {
            throw new NotImplementedException();
        }
        public override void ServerDisconnect(int connectionId)
        {
            throw new NotImplementedException();
        }
        public override string ServerGetClientAddress(int connectionId)
        {
            throw new NotImplementedException();
        }
        public override void ServerStop()
        {
            throw new NotImplementedException();
        }
        public override int GetMaxPacketSize(int channelId = Channels.Reliable)
        {
            throw new NotImplementedException();
        }
        public override void Shutdown()
        {
            throw new NotImplementedException();
        }
    }
}
