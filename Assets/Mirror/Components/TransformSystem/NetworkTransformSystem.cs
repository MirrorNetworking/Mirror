using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JamesFrowen.BitPacking;
using UnityEngine;

namespace Mirror.TransformSyncing
{
    public class NetworkTransformSystem : MonoBehaviour
    {
        // todo make this work with network Visibility
        // todo add maxMessageSize (splits up update message into multiple messages if too big)


        [Header("Reference")]
        [SerializeField] internal NetworkTransformSystemRuntimeReference runtime;

        [Header("Sync")]
        [Tooltip("How often 1 behaviour should update")]
        public float syncInterval = 0.1f;
        [Tooltip("Check if behaviours need update every frame, If false then checks every syncInterval")]
        public bool checkEveryFrame = true;

        [Header("timer Compression")]
        [SerializeField] float maxTime = 60 * 60 * 24;
        [SerializeField] float timePrecision = 1 / 1000f;

        [Header("Id Compression")]
        [SerializeField] int smallBitCount = 6;
        [SerializeField] int mediumBitCount = 12;
        [SerializeField] int largeBitCount = 18;

        [Header("Position Compression")]
        [SerializeField] Vector3 min = Vector3.one * -100;
        [SerializeField] Vector3 max = Vector3.one * 100;
        [SerializeField] float precision = 0.01f;

        [Header("Rotation Compression")]
        [SerializeField] int bitCount = 9;


        [Header("Position Debug And Gizmo")]
        // todo replace these serialized fields with custom editor
        [SerializeField] private bool drawGizmo;
        [SerializeField] private Color gizmoColor;
        [Tooltip("readonly")]
        [SerializeField] private int _bitCount;
        [Tooltip("readonly")]
        [SerializeField] private Vector3Int _bitCountAxis;
        [Tooltip("readonly")]
        [SerializeField] private int _byteCount;


        [NonSerialized] BitWriter bitWriter = new BitWriter();
        [NonSerialized] internal BitReader bitReader = new BitReader();
        [NonSerialized] internal FloatPacker timePacker;
        [NonSerialized] internal UIntVariablePacker idPacker;
        [NonSerialized] internal PositionPacker positionPacker;
        [NonSerialized] internal QuaternionPacker rotationPacker;


        [NonSerialized] float nextSyncInterval;

        private void Awake()
        {
            // time precision 1000 times more than interval
            timePacker = new FloatPacker(0, maxTime, timePrecision);
            idPacker = new UIntVariablePacker(smallBitCount, mediumBitCount, largeBitCount);
            positionPacker = new PositionPacker(min, max, precision);
            rotationPacker = new QuaternionPacker(bitCount);
        }

        private void OnEnable()
        {
            runtime.System = this;
        }
        private void OnDisable()
        {
            runtime.System = null;
        }
        private void OnValidate()
        {
            positionPacker = new PositionPacker(min, max, precision);
            _bitCount = positionPacker.bitCount;
            _bitCountAxis = positionPacker.BitCountAxis;
            _byteCount = Mathf.CeilToInt(_bitCount / 8f);
        }

        public void RegisterHandlers()
        {
            // todo find a way to register these handles so it doesn't need to be done from NetworkManager
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
            // todo find a way to unregister these handles so it doesn't need to be done from NetworkManager
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
            if (runtime.behaviours.Count == 0) { return; }

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                bool anyNeedUpdate = PackBehaviours(writer, Time.time);

                // dont send anything if nothing was written (eg, nothing dirty)
                if (!anyNeedUpdate) { return; }

                NetworkServer.SendToAll(new NetworkPositionMessage
                {
                    bytes = writer.ToArraySegment()
                });
            }
        }

        internal bool PackBehaviours(PooledNetworkWriter netWriter, float time)
        {
            bitWriter.Reset(netWriter);
            timePacker.Pack(bitWriter, time);
            bool anyNeedUpdate = false;
            foreach (KeyValuePair<uint, IHasPositionRotation> kvp in runtime.behaviours)
            {
                IHasPositionRotation behaviour = kvp.Value;
                if (!behaviour.NeedsUpdate())
                    continue;

                anyNeedUpdate = true;

                TransformState state = behaviour.State;

                idPacker.Pack(bitWriter, state.id);
                positionPacker.Pack(bitWriter, state.position);
                rotationPacker.Pack(bitWriter, state.rotation);

                // todo handle client authority updates better
                behaviour.ClearNeedsUpdate(syncInterval);
            }
            bitWriter.Flush();
            return anyNeedUpdate;
        }

        internal void ClientHandleNetworkPositionMessage(NetworkConnection _conn, NetworkPositionMessage msg)
        {
            int count = msg.bytes.Count;
            using (PooledNetworkReader netReader = NetworkReaderPool.GetReader(msg.bytes))
            {
                bitReader.Reset(netReader);
                float time = timePacker.Unpack(bitReader);

                while (netReader.Position < count)
                {
                    uint id = idPacker.Unpack(bitReader);
                    Vector3 pos = positionPacker.Unpack(bitReader);
                    Quaternion rot = rotationPacker.Unpack(bitReader);

                    if (runtime.behaviours.TryGetValue(id, out IHasPositionRotation behaviour))
                    {
                        behaviour.ApplyOnClient(new TransformState(pos, rot), time);
                    }
                }
                Debug.Assert(netReader.Position == count, "should have read exact amount");
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendMessageToServer(IHasPositionRotation behaviour)
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                bitWriter.Reset(writer);

                TransformState state = behaviour.State;

                idPacker.Pack(bitWriter, state.id);
                positionPacker.Pack(bitWriter, state.position);
                rotationPacker.Pack(bitWriter, state.rotation);

                behaviour.ClearNeedsUpdate(syncInterval);

                bitWriter.Flush();

                // dont send anything if nothing was written (eg, nothing dirty)
                if (writer.Length == 0) { return; }

                NetworkClient.Send(new NetworkPositionSingleMessage
                {
                    bytes = writer.ToArraySegment()
                });
            }
        }

        /// <summary>
        /// Position from client to server
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        internal void ServerHandleNetworkPositionMessage(NetworkConnection _conn, NetworkPositionSingleMessage msg)
        {
            using (PooledNetworkReader netReader = NetworkReaderPool.GetReader(msg.bytes))
            {
                bitReader.Reset(netReader);
                uint id = idPacker.Unpack(bitReader);
                Vector3 pos = positionPacker.Unpack(bitReader);
                Quaternion rot = rotationPacker.Unpack(bitReader);

                if (runtime.behaviours.TryGetValue(id, out IHasPositionRotation behaviour))
                {
                    behaviour.ApplyOnServer(new TransformState(pos, rot));
                }

                Debug.Assert(netReader.Position == msg.bytes.Count, "should have read exact amount");
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

    public interface IHasPositionRotation
    {
        /// <summary>
        /// ID, Position and rotation of object
        /// <para>Could be local or world position writer doesn't care</para>
        /// </summary>
        TransformState State { get; }

        bool NeedsUpdate();
        void ClearNeedsUpdate(float interval);

        /// <summary>
        /// Applies position and rotation on server
        /// </summary>
        /// <param name="state"></param>
        void ApplyOnServer(TransformState state);

        /// <summary>
        /// Applies position and rotation on server
        /// <para>this should apply interoperation so it looks smooth to the user</para>
        /// </summary>
        /// <param name="values"></param>
        void ApplyOnClient(TransformState state, float serverTime);
        bool InControl(NetworkConnection conn);
    }
    public struct TransformState
    {
        public readonly uint id;
        public readonly Vector3 position;
        public readonly Quaternion rotation;

        public TransformState(Vector3 position, Quaternion rotation)
        {
            id = default;
            this.position = position;
            this.rotation = rotation;
        }
        public TransformState(uint id, Vector3 position, Quaternion rotation)
        {
            this.id = id;
            this.position = position;
            this.rotation = rotation;
        }

        public override string ToString()
        {
            return $"[{id}, {position}, {rotation}]";
        }
    }

    public struct NetworkPositionMessage : NetworkMessage
    {
        public ArraySegment<byte> bytes;
    }
    public struct NetworkPositionSingleMessage : NetworkMessage
    {
        public ArraySegment<byte> bytes;
    }
}
