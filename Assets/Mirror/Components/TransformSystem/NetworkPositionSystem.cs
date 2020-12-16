using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror.PositionSyncing
{
    public class NetworkPositionSystem : MonoBehaviour
    {
        // todo make this work with network Visibility
        // todo replace singleton with scriptable object (find a way to read without needing static)
        // todo add maxMessageSize (splits up update message into multiple messages if too big)

        public static NetworkTransformSystem Instance { get; private set; }

        readonly Dictionary<uint, IHasPosition> _behaviours = new Dictionary<uint, IHasPosition>();

        public IReadOnlyCollection<KeyValuePair<uint, IHasPosition>> Behaviours => _behaviours;

        public void AddBehaviour(IHasPosition behaviour)
        {
            _behaviours.Add(behaviour.Id, behaviour);
        }

        public void RemoveBehaviour(IHasPosition behaviour)
        {
            _behaviours.Remove(behaviour.Id);
        }


        [Header("Sync")]
        [Tooltip("How often 1 behaviour should update")]
        public float syncInterval = 0.1f;
        [Tooltip("Check if behaviours need update every frame, If false then checks every syncInterval")]
        public bool checkEveryFrame = true;

        [Header("Position Compression")]
        [SerializeField] bool compressPosition = true;
        [SerializeField] Vector3 min = Vector3.one * -100;
        [SerializeField] Vector3 max = Vector3.one * 100;
        [SerializeField] float precision = 0.01f;


        [Header("Debug And Gizmo")]
        [SerializeField] private bool drawGizmo;
        [SerializeField] private Color gizmoColor;
        [Tooltip("readonly")]
        [SerializeField] private int _bitCount;
        [SerializeField] private Vector3Int _bitCountAxis;
        [Tooltip("readonly")]
        [SerializeField] private int _byteCount;

        // this needs to be public for reader
        [NonSerialized]
        public PositionCompression compression;

        [NonSerialized]
        float nextSyncInterval;

        private void OnValidate()
        {
            compression = new PositionCompression(min, max, precision);
            _bitCount = compression.bitCount;
            _bitCountAxis = compression.BitCountAxis;
            _byteCount = Mathf.CeilToInt(_bitCount / 8f);
        }
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            compression = new PositionCompression(min, max, precision);
        }

        public void RegisterHandlers()
        {
            if (NetworkClient.active)
            {
                NetworkClient.RegisterHandler<NetworkPositionMessage>(ClientHandleNetworkPositionMessage);
            }

            if (NetworkServer.active)
            {
                NetworkServer.RegisterHandler<NetworkPositionSingleMessage>(ServerHandleNetworkPositionMessage);
            }
        }

        public void UnregisterHandlers()
        {
            if (NetworkClient.active)
            {
                NetworkClient.UnregisterHandler<NetworkPositionMessage>();
            }

            if (NetworkServer.active)
            {
                NetworkServer.UnregisterHandler<NetworkPositionSingleMessage>();
            }
        }

        [ServerCallback]
        private void LateUpdate()
        {
            if (checkEveryFrame || ShouldSync())
            {
                SendUpdateToAll();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ShouldSync()
        {
            float now = Time.time;
            if (now > nextSyncInterval)
            {
                nextSyncInterval += syncInterval;
                return true;
            }
            else
            {
                return false;
            }
        }

        internal void SendUpdateToAll()
        {
            // dont send message if no behaviours
            if (_behaviours.Count == 0) { return; }

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                NetworkPositionMessage msg = CreateSendToAllMessage(writer);

                // dont send if none were dirty
                if (msg.bytes.Count == 0) { return; }

                NetworkServer.SendToAll(msg);
            }
        }

        internal NetworkPositionMessage CreateSendToAllMessage(NetworkWriter writer)
        {
            NetworkPositionMessage msg;
            float now = Time.time;


            foreach (KeyValuePair<uint, IHasPosition> kvp in _behaviours)
            {
                IHasPosition behaviour = kvp.Value;
                if (!behaviour.NeedsUpdate(now))
                    continue;

                uint id = kvp.Key;
                Vector3 position = behaviour.Position;

                writer.WritePackedUInt32(id);

                if (compressPosition)
                {
                    compression.Compress(writer, position);
                }
                else
                {
                    writer.WriteVector3(position);
                }
            }

            msg = new NetworkPositionMessage
            {
                bytes = writer.ToArraySegment()
            };

            return msg;
        }



        /// <summary>
        /// Position from client to server
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        internal void ServerHandleNetworkPositionMessage(NetworkConnection _conn, NetworkPositionSingleMessage msg)
        {
            uint id = msg.id;
            Vector3 position = msg.position;

            if (_behaviours.TryGetValue(id, out IHasPosition behaviour))
            {
                behaviour.SetPositionServer(position);
            }
        }


        internal void ClientHandleNetworkPositionMessage(NetworkConnection _conn, NetworkPositionMessage msg)
        {
            int count = msg.bytes.Count;
            using (PooledNetworkReader reader = NetworkReaderPool.GetReader(msg.bytes))
            {
                while (reader.Position < count)
                {
                    uint id = reader.ReadPackedUInt32();
                    Vector3 position = compressPosition
                        ? compression.Decompress(reader)
                        : reader.ReadVector3();

                    if (_behaviours.TryGetValue(id, out IHasPosition behaviour))
                    {
                        behaviour.SetPositionClient(position);
                    }
                }
                Debug.Assert(reader.Position == count, "should have read exact amount");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmo) { return; }
            Gizmos.color = gizmoColor;
            Bounds bounds = default;
            bounds.min = min;
            bounds.max = max;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
#endif
    }

    public interface IHasPosition
    {
        /// <summary>
        /// Position of object
        /// <para>Could be localposition or world position writer doesn't care</para>
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// Normally NetId, but could be a 
        /// </summary>
        uint Id { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool NeedsUpdate(float now);

        void SetPositionServer(Vector3 position);
        void SetPositionClient(Vector3 position);
    }
    public struct NetworkPositionMessage : NetworkMessage
    {
        public ArraySegment<byte> bytes;
    }
    public struct NetworkPositionSingleMessage : NetworkMessage
    {
        public uint id;
        public Vector3 position;
    }
    public static class PositionMessageWriter
    {
        //public static void WritePositionMessage(this NetworkWriter writer, NetworkPositionMessage msg)
        //{
        //    int count = msg.bytes.Count;
        //    writer.WriteUInt16((ushort)count);
        //    writer.WriteBytes(msg.bytes.Array, msg.bytes.Offset, count);
        //}
        //public static NetworkPositionMessage ReadPositionMessage(this NetworkReader reader)
        //{
        //    ushort count = reader.ReadUInt16();
        //    ArraySegment<byte> bytes = reader.ReadBytesSegment(count);

        //    return new NetworkPositionMessage
        //    {
        //        bytes = bytes
        //    };
        //}

        public static void WriteNetworkPositionSingleMessage(this NetworkWriter writer, NetworkPositionSingleMessage msg)
        {
            writer.WritePackedUInt32(msg.id);
            PositionCompression compression = NetworkTransformSystem.Instance.compression;
            compression.Compress(writer, msg.position);
        }
        public static NetworkPositionSingleMessage ReadNetworkPositionSingleMessage(this NetworkReader reader)
        {
            uint id = reader.ReadPackedUInt32();
            PositionCompression compression = NetworkTransformSystem.Instance.compression;
            Vector3 pos = compression.Decompress(reader);

            return new NetworkPositionSingleMessage
            {
                id = id,
                position = pos
            };
        }
    }
}
