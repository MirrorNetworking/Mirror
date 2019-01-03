using System;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/NetworkTransformChild")]
    public class NetworkTransformChild : NetworkBehaviour
    {
        [SerializeField]
        Transform m_Target;

        [SerializeField]
        uint m_ChildIndex;

        NetworkTransform m_Root;

        [SerializeField] NetworkTransform.AxisSyncMode          m_SyncRotationAxis = NetworkTransform.AxisSyncMode.AxisXYZ;
        [SerializeField] NetworkTransform.CompressionSyncMode   m_RotationSyncCompression = NetworkTransform.CompressionSyncMode.None;
        [SerializeField] float                                  m_MovementThreshold = 0.001f;

        [SerializeField] float                                  m_InterpolateRotation = 0.5f;
        [SerializeField] float                                  m_InterpolateMovement = 0.5f;
        [SerializeField] NetworkTransform.ClientMoveCallback3D  m_ClientMoveCallback3D;

        // movement smoothing
        Vector3         m_TargetSyncPosition;
        Quaternion      m_TargetSyncRotation3D;

        float           m_LastClientSyncTime; // last time client received a sync from server
        float           m_LastClientSendTime; // last time client send a sync to server

        Vector3         m_PrevPosition;
        Quaternion      m_PrevRotation;

        const float     k_LocalMovementThreshold = 0.00001f;
        const float     k_LocalRotationThreshold = 0.00001f;

        // settings
        public Transform                            target { get {return m_Target; } set { m_Target = value; OnValidate(); } }
        public uint                                 childIndex { get { return m_ChildIndex; }}
        public NetworkTransform.AxisSyncMode        syncRotationAxis { get { return m_SyncRotationAxis; } set { m_SyncRotationAxis = value; } }
        public NetworkTransform.CompressionSyncMode rotationSyncCompression { get { return m_RotationSyncCompression; } set { m_RotationSyncCompression = value; } }
        public float                                movementThreshold { get { return m_MovementThreshold; } set { m_MovementThreshold = value; } }
        public float                                interpolateRotation { get { return m_InterpolateRotation; } set { m_InterpolateRotation = value; } }
        public float                                interpolateMovement { get { return m_InterpolateMovement; } set { m_InterpolateMovement = value; } }
        public NetworkTransform.ClientMoveCallback3D clientMoveCallback3D { get { return m_ClientMoveCallback3D; } set { m_ClientMoveCallback3D = value; } }

        // runtime data
        public float                lastSyncTime { get { return m_LastClientSyncTime; } }
        public Vector3              targetSyncPosition { get { return m_TargetSyncPosition; } }
        public Quaternion           targetSyncRotation3D { get { return m_TargetSyncRotation3D; } }

        void OnValidate()
        {
            // root parent of target must have a NetworkTransform
            if (m_Target != null)
            {
                Transform parent = m_Target.parent;
                if (parent == null)
                {
                    Debug.LogError("NetworkTransformChild target cannot be the root transform.");
                    m_Target = null;
                    return;
                }
                while (parent.parent != null)
                {
                    parent = parent.parent;
                }

                m_Root = parent.gameObject.GetComponent<NetworkTransform>();
                if (m_Root == null)
                {
                    Debug.LogError("NetworkTransformChild root must have NetworkTransform");
                    m_Target = null;
                    return;
                }
            }

            if (m_Root != null)
            {
                // childIndex is the index within all the NetworkChildTransforms on the root
                m_ChildIndex = UInt32.MaxValue;
                var childTransforms = m_Root.GetComponents<NetworkTransformChild>();
                for (uint i = 0; i < childTransforms.Length; i++)
                {
                    if (childTransforms[i] == this)
                    {
                        m_ChildIndex = i;
                        break;
                    }
                }
                if (m_ChildIndex == UInt32.MaxValue)
                {
                    Debug.LogError("NetworkTransformChild component must be a child in the same hierarchy");
                    m_Target = null;
                }
            }

            if (m_SyncRotationAxis < NetworkTransform.AxisSyncMode.None || m_SyncRotationAxis > NetworkTransform.AxisSyncMode.AxisXYZ)
            {
                m_SyncRotationAxis = NetworkTransform.AxisSyncMode.None;
            }

            if (movementThreshold < 0)
            {
                movementThreshold = 0.00f;
            }

            if (interpolateRotation < 0)
            {
                interpolateRotation = 0.01f;
            }
            if (interpolateRotation > 1.0f)
            {
                interpolateRotation = 1.0f;
            }

            if (interpolateMovement < 0)
            {
                interpolateMovement  = 0.01f;
            }
            if (interpolateMovement > 1.0f)
            {
                interpolateMovement = 1.0f;
            }
        }

        void Awake()
        {
            m_PrevPosition = m_Target.localPosition;
            m_PrevRotation = m_Target.localRotation;
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (initialState)
            {
                // always write initial state, no dirty bits
            }
            else if (syncVarDirtyBits == 0)
            {
                writer.WritePackedUInt32(0);
                return false;
            }
            else
            {
                // dirty bits
                writer.WritePackedUInt32(1);
            }

            SerializeModeTransform(writer);
            return true;
        }

        void SerializeModeTransform(NetworkWriter writer)
        {
            // position
            writer.Write(m_Target.localPosition);

            // rotation
            if (m_SyncRotationAxis != NetworkTransform.AxisSyncMode.None)
            {
                NetworkTransform.SerializeRotation3D(writer, m_Target.localRotation, syncRotationAxis, rotationSyncCompression);
            }
            m_PrevPosition = m_Target.localPosition;
            m_PrevRotation = m_Target.localRotation;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (isServer && NetworkServer.localClientActive)
                return;

            if (!initialState)
            {
                if (reader.ReadPackedUInt32() == 0)
                    return;
            }
            UnserializeModeTransform(reader, initialState);

            m_LastClientSyncTime = Time.time;
        }

        void UnserializeModeTransform(NetworkReader reader, bool initialState)
        {
            if (hasAuthority)
            {
                // this component must read the data that the server wrote, even if it ignores it.
                // otherwise the NetworkReader stream will still contain that data for the next component.

                // position
                reader.ReadVector3();

                if (syncRotationAxis != NetworkTransform.AxisSyncMode.None)
                {
                    NetworkTransform.UnserializeRotation3D(reader, syncRotationAxis, rotationSyncCompression);
                }
                return;
            }

            if (isServer && m_ClientMoveCallback3D != null)
            {
                var pos = reader.ReadVector3();
                var vel = Vector3.zero;
                var rot = Quaternion.identity;
                if (syncRotationAxis != NetworkTransform.AxisSyncMode.None)
                {
                    rot = NetworkTransform.UnserializeRotation3D(reader, syncRotationAxis, rotationSyncCompression);
                }

                if (m_ClientMoveCallback3D(ref pos, ref vel, ref rot))
                {
                    m_TargetSyncPosition = pos;
                    if (syncRotationAxis != NetworkTransform.AxisSyncMode.None)
                    {
                        m_TargetSyncRotation3D = rot;
                    }
                }
            }
            else
            {
                // position
                m_TargetSyncPosition = reader.ReadVector3();

                // rotation
                if (syncRotationAxis != NetworkTransform.AxisSyncMode.None)
                {
                    m_TargetSyncRotation3D = NetworkTransform.UnserializeRotation3D(reader, syncRotationAxis, rotationSyncCompression);
                }
            }
        }

        void FixedUpdate()
        {
            if (isServer)
            {
                FixedUpdateServer();
            }
            if (isClient)
            {
                FixedUpdateClient();
            }
        }

        void FixedUpdateServer()
        {
            if (syncVarDirtyBits != 0)
                return;

            // dont run if network isn't active
            if (!NetworkServer.active)
                return;

            // dont run if we haven't been spawned yet
            if (!isServer)
                return;

            // dont' auto-dirty if no send interval
            if (syncInterval == 0)
                return;

            float distance = (m_Target.localPosition - m_PrevPosition).sqrMagnitude;
            if (distance < movementThreshold)
            {
                distance = Quaternion.Angle(m_PrevRotation, m_Target.localRotation);
                if (distance < movementThreshold)
                {
                    return;
                }
            }

            // This will cause transform to be sent
            SetDirtyBit(1);
        }

        void FixedUpdateClient()
        {
            // dont run if we haven't received any sync data
            if (m_LastClientSyncTime == 0)
                return;

            // dont run if network isn't active
            if (!NetworkServer.active && !NetworkClient.active)
                return;

            // dont run if we haven't been spawned yet
            if (!isServer && !isClient)
                return;

            // dont run if not expecting continuous updates
            if (syncInterval == 0)
                return;

            // dont run this if this client has authority over this player object
            if (hasAuthority)
                return;

            // interpolate on client
            if (m_LastClientSyncTime != 0)
            {
                m_Target.localPosition = m_InterpolateMovement > 0
                    ? Vector3.Lerp(m_Target.localPosition, m_TargetSyncPosition, m_InterpolateMovement)
                    : m_TargetSyncPosition;

                m_Target.localRotation = m_InterpolateRotation > 0
                    ? Quaternion.Slerp(m_Target.localRotation, m_TargetSyncRotation3D, m_InterpolateRotation)
                    : m_TargetSyncRotation3D;
            }
        }

        // --------------------- local transform sync  ------------------------

        void Update()
        {
            if (!hasAuthority)
                return;

            if (!localPlayerAuthority)
                return;

            if (NetworkServer.active)
                return;

            if (Time.time - m_LastClientSendTime > syncInterval)
            {
                SendTransform();
                m_LastClientSendTime = Time.time;
            }
        }

        bool HasMoved()
        {
            float diff = 0;

            // check if position has changed
            diff = (m_Target.localPosition - m_PrevPosition).sqrMagnitude;
            if (diff > k_LocalMovementThreshold)
            {
                return true;
            }

            // check if rotation has changed
            diff = Quaternion.Angle(m_Target.localRotation, m_PrevRotation);
            if (diff > k_LocalRotationThreshold)
            {
                return true;
            }

            // check if velocty has changed

            return false;
        }

        [Client]
        void SendTransform()
        {
            if (!HasMoved() || ClientScene.readyConnection == null)
            {
                return;
            }

            NetworkWriter writer = new NetworkWriter();
            SerializeModeTransform(writer);

            LocalChildTransformMessage message = new LocalChildTransformMessage();
            message.netId = netId;
            message.childIndex = m_ChildIndex;
            message.payload = writer.ToArray();

            m_PrevPosition = m_Target.localPosition;
            m_PrevRotation = m_Target.localRotation;

            ClientScene.readyConnection.Send((short)MsgType.LocalChildTransform, message);
        }

        internal static void HandleChildTransform(NetworkMessage netMsg)
        {
            LocalChildTransformMessage message = netMsg.ReadMessage<LocalChildTransformMessage>();

            GameObject foundObj = NetworkServer.FindLocalObject(message.netId);
            if (foundObj == null)
            {
                Debug.LogError("Received NetworkTransformChild data for GameObject that doesn't exist");
                return;
            }
            var children = foundObj.GetComponents<NetworkTransformChild>();
            if (children == null || children.Length == 0)
            {
                Debug.LogError("HandleChildTransform no children");
                return;
            }
            if (message.childIndex >= children.Length)
            {
                Debug.LogError("HandleChildTransform childIndex invalid");
                return;
            }

            NetworkTransformChild foundSync = children[message.childIndex];
            if (foundSync == null)
            {
                Debug.LogError("HandleChildTransform null target");
                return;
            }
            if (!foundSync.localPlayerAuthority)
            {
                Debug.LogError("HandleChildTransform no localPlayerAuthority");
                return;
            }

            if (!netMsg.conn.clientOwnedObjects.Contains(message.netId))
            {
                Debug.LogWarning("NetworkTransformChild netId:" + message.netId + " is not for a valid player");
                return;
            }

            foundSync.UnserializeModeTransform(new NetworkReader(message.payload), false);
            foundSync.m_LastClientSyncTime = Time.time;

            if (!foundSync.isClient)
            {
                // dedicated server wont interpolate, so snap.
                foundSync.m_Target.localPosition = foundSync.m_TargetSyncPosition;
                foundSync.m_Target.localRotation = foundSync.m_TargetSyncRotation3D;
            }
        }
    }
}
