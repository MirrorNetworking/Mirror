using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

/// <summary>
/// This file contains all classes with deprecated methods and properties.
/// We created this as a convenient reference, and to make it easier for users
/// migrating from UNet to Mirror.  This is the only file that is packed with
/// multiple classes, otherwise we follow the practice of one class per file in
/// the project.
/// 
/// In many cases the methods here are written to directly call their replacements
/// so you likely can copy that code to your own project to clear the obsolete warnings.
/// 
/// Those wishing to submit a PR that would obsolete something in a core class must move
/// the obsoleted property or method to this file with appropriate comments and include
/// the changes here with the PR.
/// </summary>
namespace Mirror
{
    // built-in system network messages
    // original HLAPI uses short, so let's keep short to not break packet header etc.
    // => use .ToString() to get the field name from the field value
    // => we specify the short values so it's easier to look up opcodes when debugging packets
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Use Send<T>  with no message id instead")]
    public enum MsgType : short
    {
        // internal system messages - cannot be replaced by user code
        ObjectDestroy = 1,
        Rpc = 2,
        Owner = 4,
        Command = 5,
        SyncEvent = 7,
        UpdateVars = 8,
        SpawnPrefab = 3,
        SpawnSceneObject = 10,
        SpawnStarted = 11,
        SpawnFinished = 12,
        ObjectHide = 13,
        LocalClientAuthority = 15,

        // public system messages - can be replaced by user code
        Connect = 32,
        Disconnect = 33,
        Error = 34,
        Ready = 35,
        NotReady = 36,
        AddPlayer = 37,
        RemovePlayer = 38,
        Scene = 39,

        // time synchronization
        Ping = 43,
        Pong = 44,

        Highest = 47
    }

    public static partial class ClientScene
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use NetworkIdentity.spawned[netId] instead.")]
        public static GameObject FindLocalObject(uint netId)
        {
            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
            {
                return identity.gameObject;
            }
            return null;
        }
    }

    public static partial class MessagePacker
    {
        // pack message before sending
        // -> pass writer instead of byte[] so we can reuse it
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use Pack<T> instead")]
        public static byte[] PackMessage(int msgType, MessageBase msg)
        {
            // reset cached writer length and position
            packWriter.SetLength(0);

            // write message type
            packWriter.Write((short)msgType);

            // serialize message into writer
            msg.Serialize(packWriter);

            // return byte[]
            return packWriter.ToArray();
        }
    }

    public partial class NetworkClient
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use NetworkClient directly. Singleton isn't needed anymore, all functions are static now. For example: NetworkClient.Send(message) instead of NetworkClient.singleton.Send(message).")]
        public static NetworkClient singleton = new NetworkClient();

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use NetworkClient directly instead. There is always exactly one client.")]
        public static List<NetworkClient> allClients => new List<NetworkClient> { singleton };

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use SendMessage<T> instead with no message id instead")]
        public static bool Send(short msgType, MessageBase msg)
        {
            if (connection != null)
            {
                if (connectState != ConnectState.Connected)
                {
                    Debug.LogError("NetworkClient Send when not connected to a server");
                    return false;
                }
                return connection.Send(msgType, msg);
            }
            Debug.LogError("NetworkClient Send with no connection");
            return false;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use NetworkTime.rtt instead")]
        public static float GetRTT()
        {
            return (float)NetworkTime.rtt;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(int msgType, NetworkMessageDelegate handler)
        {
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkClient.RegisterHandler replacing " + handler.ToString() + " - " + msgType);
            }
            handlers[msgType] = handler;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((int)msgType, handler);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(int msgType)
        {
            handlers.Remove(msgType);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((int)msgType);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Call NetworkClient.Shutdown() instead. There is only one client.")]
        public static void ShutdownAll()
        {
            Shutdown();
        }
    }

    public partial class NetworkConnection
    {
        // this is always true for regular connections, false for local
        // connections because it's set in the constructor and never reset.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("isConnected will be removed because it's pointless. A NetworkConnection is always connected.")]
        public bool isConnected { get; protected set; }

        // this is always 0 for regular connections, -1 for local
        // connections because it's set in the constructor and never reset.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("hostId will be removed because it's not needed ever since we removed LLAPI as default. It's always 0 for regular connections and -1 for local connections. Use connection.GetType() == typeof(NetworkConnection) to check if it's a regular or local connection.")]
        public int hostId = -1;

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("use Send<T> instead")]
        public virtual bool Send(int msgType, MessageBase msg, int channelId = Channels.DefaultReliable)
        {
            // pack message and send
            byte[] message = MessagePacker.PackMessage(msgType, msg);
            return SendBytes(message, channelId);
        }
    }

    public partial class NetworkManager
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use NetworkClient directly, it will be made static soon. For example, use NetworkClient.Send(message) instead of NetworkManager.client.Send(message)")]
        public NetworkClient client => NetworkClient.singleton;

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use NetworkClient.isConnected instead")]
        public bool IsClientConnected()
        {
            return NetworkClient.isConnected;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use OnServerAddPlayer(NetworkConnection conn, AddPlayerMessage extraMessage) instead")]
        public virtual void OnServerAddPlayer(NetworkConnection conn, NetworkMessage extraMessage)
        {
            OnServerAddPlayer(conn, null);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use OnServerAddPlayer(NetworkConnection conn, AddPlayerMessage extraMessage) instead")]
        public virtual void OnServerAddPlayer(NetworkConnection conn)
        {
            OnServerAddPlayer(conn, null);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use OnStartClient() instead of OnStartClient(NetworkClient client). All NetworkClient functions are static now, so you can use NetworkClient.Send(message) instead of client.Send(message) directly now.")]
        public virtual void OnStartClient(NetworkClient client) { }
    }

    public static partial class NetworkServer
    {
        // this is like SendToReady - but it doesn't check the ready flag on the connection.
        // this is used for ObjectDestroy messages.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("use SendToObservers<T> instead")]
        static bool SendToObservers(NetworkIdentity identity, short msgType, MessageBase msg)
        {
            if (LogFilter.Debug) Debug.Log("Server.SendToObservers id:" + msgType);

            if (identity != null && identity.observers != null)
            {
                // pack message into byte[] once
                byte[] bytes = MessagePacker.PackMessage((ushort)msgType, msg);

                // send to all observers
                bool result = true;
                foreach (KeyValuePair<int, NetworkConnection> kvp in identity.observers)
                {
                    result &= kvp.Value.SendBytes(bytes);
                }
                return result;
            }
            return false;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use SendToAll<T> instead")]
        public static bool SendToAll(int msgType, MessageBase msg, int channelId = Channels.DefaultReliable)
        {
            if (LogFilter.Debug) Debug.Log("Server.SendToAll id:" + msgType);

            // pack message into byte[] once
            byte[] bytes = MessagePacker.PackMessage((ushort)msgType, msg);

            // send to all
            bool result = true;
            foreach (KeyValuePair<int, NetworkConnection> kvp in connections)
            {
                result &= kvp.Value.SendBytes(bytes, channelId);
            }
            return result;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use SendToReady<T> instead")]
        public static bool SendToReady(NetworkIdentity identity, short msgType, MessageBase msg, int channelId = Channels.DefaultReliable)
        {
            if (LogFilter.Debug) Debug.Log("Server.SendToReady msgType:" + msgType);

            if (identity != null && identity.observers != null)
            {
                // pack message into byte[] once
                byte[] bytes = MessagePacker.PackMessage((ushort)msgType, msg);

                // send to all ready observers
                bool result = true;
                foreach (KeyValuePair<int, NetworkConnection> kvp in identity.observers)
                {
                    if (kvp.Value.isReady)
                    {
                        result &= kvp.Value.SendBytes(bytes, channelId);
                    }
                }
                return result;
            }
            return false;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(int msgType, NetworkMessageDelegate handler)
        {
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkServer.RegisterHandler replacing " + msgType);
            }
            handlers[msgType] = handler;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((int)msgType, handler);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(int msgType)
        {
            handlers.Remove(msgType);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((int)msgType);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use SendToClient<T> instead")]
        public static void SendToClient(int connectionId, int msgType, MessageBase msg)
        {
            if (connections.TryGetValue(connectionId, out NetworkConnection conn))
            {
                conn.Send(msgType, msg);
                return;
            }
            Debug.LogError("Failed to send message to connection ID '" + connectionId + ", not found in connection list");
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use SendToClientOfPlayer<T> instead")]
        public static void SendToClientOfPlayer(NetworkIdentity identity, int msgType, MessageBase msg)
        {
            if (identity != null)
            {
                identity.connectionToClient.Send(msgType, msg);
            }
            else
            {
                Debug.LogError("SendToClientOfPlayer: player has no NetworkIdentity: " + identity.name);
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use NetworkIdentity.spawned[netId] instead.")]
        public static GameObject FindLocalObject(uint netId)
        {
            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
            {
                return identity.gameObject;
            }
            return null;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Use NetworkBehaviour.syncInterval field instead. Can be modified in the Inspector too.")]
    [AttributeUsage(AttributeTargets.Class)]
    public class NetworkSettingsAttribute : Attribute
    {
        public float sendInterval = 0.1f;
    }

    public abstract partial class Transport
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use ServerGetClientAddress(int connectionId) instead")]
        public virtual bool GetConnectionInfo(int connectionId, out string address)
        {
            address = ServerGetClientAddress(connectionId);
            return true;
        }
    }
}
