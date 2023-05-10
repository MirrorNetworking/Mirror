using System;

namespace Mirror.Tests.NetworkIdentities
{
    class StartServerExceptionNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStartServer()
        {
            ++called;
            throw new Exception("some exception");
        }
    }

    class StartClientExceptionNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStartClient()
        {
            ++called;
            throw new Exception("some exception");
        }
    }

    class StartAuthorityExceptionNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStartAuthority()
        {
            ++called;
            throw new Exception("some exception");
        }
    }

    class StopAuthorityExceptionNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStopAuthority()
        {
            ++called;
            throw new Exception("some exception");
        }
    }

    class StartLocalPlayerExceptionNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStartLocalPlayer()
        {
            ++called;
            throw new Exception("some exception");
        }
    }

    class StopClientExceptionNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStopClient()
        {
            ++called;
            throw new Exception("some exception");
        }
    }

    class StopLocalPlayerExceptionNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStopLocalPlayer()
        {
            ++called;
            throw new Exception("some exception");
        }
    }

    class StopServerExceptionNetworkBehaviour : NetworkBehaviour
    {
        public int called;
        public override void OnStopServer()
        {
            ++called;
            throw new Exception("some exception");
        }
    }

    class SerializeTest1NetworkBehaviour : NetworkBehaviour
    {
        public int value;
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteInt(value);
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            value = reader.ReadInt();
        }
    }

    class SerializeTest2NetworkBehaviour : NetworkBehaviour
    {
        public string value;
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteString(value);
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            value = reader.ReadString();
        }
    }

    class SyncVarTest1NetworkBehaviour : NetworkBehaviour
    {
        [SyncVar] public int value;
    }

    class SyncVarTest2NetworkBehaviour : NetworkBehaviour
    {
        [SyncVar] public string value;
    }

    class SerializeExceptionNetworkBehaviour : NetworkBehaviour
    {
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            throw new Exception("some exception");
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            throw new Exception("some exception");
        }
    }

    class SerializeMismatchNetworkBehaviour : NetworkBehaviour
    {
        public int value;
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteInt(value);
            // one too many
            writer.WriteInt(value);
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            value = reader.ReadInt();
        }
    }

    class IsClientServerCheckComponent : NetworkBehaviour
    {
        // OnStartClient
        internal bool OnStartClient_isClient;
        internal bool OnStartClient_isServer;
        internal bool OnStartClient_isLocalPlayer;
        public override void OnStartClient()
        {
            OnStartClient_isClient = isClient;
            OnStartClient_isServer = isServer;
            OnStartClient_isLocalPlayer = isLocalPlayer;
        }

        // OnStartServer
        internal bool OnStartServer_isClient;
        internal bool OnStartServer_isServer;
        internal bool OnStartServer_isLocalPlayer;
        public override void OnStartServer()
        {
            OnStartServer_isClient = isClient;
            OnStartServer_isServer = isServer;
            OnStartServer_isLocalPlayer = isLocalPlayer;
        }

        // OnStartLocalPlayer
        internal bool OnStartLocalPlayer_isClient;
        internal bool OnStartLocalPlayer_isServer;
        internal bool OnStartLocalPlayer_isLocalPlayer;
        public override void OnStartLocalPlayer()
        {
            OnStartLocalPlayer_isClient = isClient;
            OnStartLocalPlayer_isServer = isServer;
            OnStartLocalPlayer_isLocalPlayer = isLocalPlayer;
        }

        // Start
        internal bool Start_isClient;
        internal bool Start_isServer;
        internal bool Start_isLocalPlayer;
        public void Start()
        {
            Start_isClient = isClient;
            Start_isServer = isServer;
            Start_isLocalPlayer = isLocalPlayer;
        }

        // OnDestroy
        internal bool OnDestroy_isClient;
        internal bool OnDestroy_isServer;
        internal bool OnDestroy_isLocalPlayer;
        public void OnDestroy()
        {
            OnDestroy_isClient = isClient;
            OnDestroy_isServer = isServer;
            OnDestroy_isLocalPlayer = isLocalPlayer;
        }
    }
}
