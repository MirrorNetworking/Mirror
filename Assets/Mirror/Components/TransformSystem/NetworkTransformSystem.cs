using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror.TransformSyncing
{
    public class NetworkTransformSystem : MonoBehaviour
    {
        // todo make this work with network Visibility
        // todo replace singleton with scriptable object (find a way to read without needing static)
        // todo add maxMessageSize (splits up update message into multiple messages if too big)

        public static NetworkTransformSystem Instance { get; private set; }

        readonly Dictionary<uint, IHasPositionRotation> _behaviours = new Dictionary<uint, IHasPositionRotation>();

        public IReadOnlyCollection<KeyValuePair<uint, IHasPositionRotation>> Behaviours => _behaviours;

        public void AddBehaviour(IHasPositionRotation behaviour)
        {
            _behaviours.Add(behaviour.Id, behaviour);
        }

        public void RemoveBehaviour(IHasPositionRotation behaviour)
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


            foreach (KeyValuePair<uint, IHasPositionRotation> kvp in _behaviours)
            {
                IHasPositionRotation behaviour = kvp.Value;
                if (!behaviour.NeedsUpdate(now))
                    continue;

                uint id = kvp.Key;
                PositionRotation posRot = behaviour.PositionRotation;

                PackedWriter.WritePacked(writer, id);

                if (compressPosition)
                {
                    compression.Compress(writer, posRot.position);
                }
                else
                {
                    writer.WriteVector3(posRot.position);
                }

                writer.WriteBlittable(Compression.CompressQuaternion(posRot.rotation));

                behaviour.ClearNeedsUpdate();
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
            Quaternion rotation = Compression.DecompressQuaternion(msg.compressedRotation);

            if (_behaviours.TryGetValue(id, out IHasPositionRotation behaviour))
            {
                behaviour.ApplyOnServer(new PositionRotation(position, rotation));
            }
        }


        internal void ClientHandleNetworkPositionMessage(NetworkConnection _conn, NetworkPositionMessage msg)
        {
            int count = msg.bytes.Count;
            using (PooledNetworkReader reader = NetworkReaderPool.GetReader(msg.bytes))
            {
                while (reader.Position < count)
                {
                    uint id = PackedWriter.ReadPacked(reader);
                    Vector3 position = compressPosition
                        ? compression.Decompress(reader)
                        : reader.ReadVector3();

                    Quaternion rotation = Compression.DecompressQuaternion(reader.ReadBlittable<uint>());

                    if (_behaviours.TryGetValue(id, out IHasPositionRotation behaviour))
                    {
                        behaviour.ApplyOnClient(new PositionRotation(position, rotation));
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

    /// <summary>
    /// packed read/write from mirror v26 and optimized
    /// </summary>
    public static class PackedWriter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePacked(NetworkWriter writer, uint value)
        {
            if (value <= 240)
            {
                writer.WriteByte((byte)value);
                return;
            }
            if (value <= 2287)
            {
                writer.WriteByte((byte)(((value - 240) >> 8) + 241));
                writer.WriteByte((byte)(value - 240));
                return;
            }
            if (value <= 67823)
            {
                writer.WriteByte(249);
                writer.WriteByte((byte)((value - 2288) >> 8));
                writer.WriteByte((byte)(value - 2288));
                return;
            }
            if (value <= 16777215)
            {
                writer.WriteByte(250);
                writer.WriteByte((byte)value);
                writer.WriteByte((byte)(value >> 8));
                writer.WriteByte((byte)(value >> 16));
                return;
            }
            if (value <= 4294967295)
            {
                writer.WriteByte(251);
                writer.WriteByte((byte)value);
                writer.WriteByte((byte)(value >> 8));
                writer.WriteByte((byte)(value >> 16));
                writer.WriteByte((byte)(value >> 24));
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadPacked(NetworkReader reader)
        {
            byte a0 = reader.ReadByte();
            if (a0 < 241)
            {
                return a0;
            }

            byte a1 = reader.ReadByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return 240 + ((a0 - (uint)241) << 8) + a1;
            }

            byte a2 = reader.ReadByte();
            if (a0 == 249)
            {
                return 2288 + ((uint)a1 << 8) + a2;
            }

            byte a3 = reader.ReadByte();
            if (a0 == 250)
            {
                return a1 + (((uint)a2) << 8) + (((uint)a3) << 16);
            }

            byte a4 = reader.ReadByte();
            if (a0 == 251)
            {
                return a1 + (((uint)a2) << 8) + (((uint)a3) << 16) + (((uint)a4) << 24);
            }

            throw new DataMisalignedException("ReadPacked() failure: " + a0);
        }
    }
    public interface IHasPositionRotation
    {
        /// <summary>
        /// Position and rotation of object
        /// <para>Could be localposition or world position writer doesn't care</para>
        /// </summary>
        PositionRotation PositionRotation { get; }

        /// <summary>
        /// Normally NetId, but could be a 
        /// </summary>
        uint Id { get; }

        bool NeedsUpdate(float now);
        void ClearNeedsUpdate();

        /// <summary>
        /// Applies position and rotation on server
        /// </summary>
        /// <param name="values"></param>
        void ApplyOnServer(PositionRotation values);

        /// <summary>
        /// Applies position and rotation on server
        /// <para>this should apply interoperation so it looks smooth to the user</para>
        /// </summary>
        /// <param name="values"></param>
        void ApplyOnClient(PositionRotation values);
    }

    public struct PositionRotation
    {
        public readonly Vector3 position;

        public readonly Quaternion rotation;

        public PositionRotation(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }

    public struct NetworkPositionMessage : NetworkMessage
    {
        public ArraySegment<byte> bytes;
    }
    public struct NetworkPositionSingleMessage : NetworkMessage
    {
        public uint id;
        public Vector3 position;
        public uint compressedRotation;

        public NetworkReader reader;
        public PositionCompression compression;

        public NetworkPositionSingleMessage FromReader(PositionCompression compression)
        {
            uint id = PackedWriter.ReadPacked(reader);
            Vector3 pos = compression.Decompress(reader);
            uint rot = reader.ReadUInt32();

            return new NetworkPositionSingleMessage
            {
                id = id,
                position = pos,
                compressedRotation = rot,
            };
        }
    }
    public static class PositionMessageWriter
    {
        public static void WriteNetworkPositionSingleMessage_static(this NetworkWriter writer, NetworkPositionSingleMessage msg)
        {
            PackedWriter.WritePacked(writer, msg.id);
            PositionCompression compression = NetworkTransformSystem.Instance.compression;
            compression.Compress(writer, msg.position);
            writer.WriteUInt32(msg.compressedRotation);

        }
        public static NetworkPositionSingleMessage ReadNetworkPositionSingleMessage_static(this NetworkReader reader)
        {
            uint id = PackedWriter.ReadPacked(reader);
            PositionCompression compression = NetworkTransformSystem.Instance.compression;
            Vector3 pos = compression.Decompress(reader);
            uint compressedRotation = reader.ReadUInt32();

            return new NetworkPositionSingleMessage
            {
                id = id,
                position = pos,
                compressedRotation = compressedRotation
            };
        }

        public static void WriteNetworkPositionSingleMessage(this NetworkWriter writer, NetworkPositionSingleMessage msg)
        {
            PackedWriter.WritePacked(writer, msg.id);
            PositionCompression compression = msg.compression;
            compression.Compress(writer, msg.position);
            writer.WriteUInt32(msg.compressedRotation);
        }
        public static NetworkPositionSingleMessage ReadNetworkPositionSingleMessage(this NetworkReader reader)
        {
            return new NetworkPositionSingleMessage
            {
                reader = reader,
            };
        }
    }
}
