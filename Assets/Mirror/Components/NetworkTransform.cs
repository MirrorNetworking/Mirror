using System.Runtime.CompilerServices;
using Mirror.TransformSyncing;
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkTransform")]
    [HelpURL("https://mirror-networking.com/docs/Articles/Components/NetworkTransform.html")]
    public class NetworkTransform : NetworkBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger<NetworkTransform>(LogType.Error);

        [Tooltip("Which transform to sync")]
        [SerializeField] Transform target;

        [Header("Authority")]
        [Tooltip("Set to true if moves come from owner client, set to false if moves always come from server")]
        [SerializeField] bool clientAuthority;

        [Tooltip("If true uses local position and rotation, if value uses world position and rotation")]
        [SerializeField] bool useLocalSpace = true;

        // todo make 0 Sensitivity always send (and avoid doing distance/angle check)
        [Tooltip("How far position has to move before it is synced")]
        [SerializeField] float positionSensitivity = 0.1f;

        [Tooltip("How far rotation has to move before it is synced")]
        [SerializeField] float rotationSensitivity = 0.1f;

        [Header("Snapshot Interpolation")]
        [Tooltip("Delay to add to client time to make sure there is always a snapshot to interpolate towards. High delay can handle more jitter, but adds latancy to the position.")]
        [SerializeField] float clientDelay = 0.1f;

        [Tooltip("Client Authority Sync Interval")]
        [SerializeField] float clientSyncInterval = 0.1f;
        [Tooltip("remove old snapshots from buffer older than SyncInterval * this value")]
        [SerializeField] float snapshotRemoveTime = 1;
        [Tooltip("How many snapshots to leave in buffer")]
        [SerializeField] int minimumSnapsots = 1;

        [SerializeField] bool showDebugGui;

        double localTime;

        /// <summary>
        /// Set when client with authority updates the server
        /// </summary>
        bool _needsUpdate;
        float _nextSyncInterval;

        // server
        Vector3 lastPosition;
        Quaternion lastRotation;

        // client
        SnapshotBuffer snapshotBuffer = new SnapshotBuffer();

        private void OnGUI()
        {
            if (showDebugGui)
            {
                GUILayout.Label($"ServerTime: {NetworkTime.time}");
                GUILayout.Label(snapshotBuffer.ToString());
            }
        }

        private void OnValidate()
        {
            if (target == null)
                target = transform;
        }

        public bool IsControlledByServer
        {
            // server auth or no owner
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !clientAuthority || connectionToClient == null;
        }
        public bool IsLocalClientInControl
        {
            // client auth and owner
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => clientAuthority && hasAuthority;
        }

        public Vector3 Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return useLocalSpace ? target.localPosition : target.position;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (useLocalSpace)
                {
                    target.localPosition = value;
                }
                else
                {
                    target.position = value;
                }
            }
        }
        public Quaternion Rotation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return useLocalSpace ? target.localRotation : target.rotation;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (useLocalSpace)
                {
                    target.localRotation = value;
                }
                else
                {
                    target.rotation = value;
                }
            }
        }

        TransformState State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new TransformState(Position, Rotation);
        }


        /// <summary>
        /// Checks if object needs syncing to clients
        /// <para>Called on server</para>
        /// </summary>
        /// <returns></returns>
        bool NeedsUpdate()
        {
            if (clientAuthority)
            {
                return _needsUpdate && TimeToUpdate;
            }
            else
            {
                return TimeToUpdate && (HasMoved() || HasRotated());
            }
        }

        bool TimeToUpdate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Time.time > _nextSyncInterval;
        }

        /// <summary>
        /// Resets values, called after syncing to client
        /// <para>Called on server</para>
        /// </summary>
        public void ClearNeedsUpdate(float interval)
        {
            _needsUpdate = false;
            _nextSyncInterval = Time.time + interval;
            lastPosition = Position;
            lastRotation = Rotation;
        }


        /// <summary>
        /// Is the connection in control of this object? is is allowed to apply the values on the server
        /// </summary>
        /// <param name="conn"></param>
        /// <returns></returns>
        bool InControl(NetworkConnection conn)
        {
            return clientAuthority && connectionToClient == conn;
        }

        /// <summary>
        /// Applies values to target transform on server
        /// <para>no need to interpolate on server</para>
        /// </summary>
        /// <param name="state"></param>
        void ApplyOnServer(TransformState state)
        {
            Position = state.position;
            Rotation = state.rotation;
            _needsUpdate = true;
        }

        /// <summary>
        /// Applies values to target transform on client
        /// <para>Adds to buffer for interpolation</para>
        /// </summary>
        /// <param name="state"></param>
        void ApplyOnClient(TransformState state, double serverTime)
        {
            // dont apply on local owner
            if (IsLocalClientInControl)
                return;

            snapshotBuffer.AddSnapShot(state, serverTime);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SendMessageToServer()
        {
            CmdClientAuthoritySync(State, NetworkTime.time);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SendMessageToClient()
        {
            RpcServerSync(State, NetworkTime.time);
        }


        /// <summary>
        /// Has target moved since we last checked
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool HasMoved()
        {
            bool moved = Vector3.Distance(lastPosition, Position) > positionSensitivity;

            if (moved)
            {
                lastPosition = Position;
            }
            return moved;
        }

        /// <summary>
        /// Has target moved since we last checked
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool HasRotated()
        {
            bool rotated = Quaternion.Angle(lastRotation, Rotation) > rotationSensitivity;

            if (rotated)
            {
                lastRotation = Rotation;
            }
            return rotated;
        }

        private void Update()
        {
            if (isServer)
            {
                ServerSyncUpdate();
            }

            if (isClient)
            {
                if (IsLocalClientInControl)
                {
                    ClientAuthorityUpdate();
                }
                else
                {
                    ClientInterpolation();
                }
            }
        }

        private void ServerSyncUpdate()
        {
            if (NeedsUpdate())
            {
                SendMessageToClient();
                ClearNeedsUpdate(clientSyncInterval);
            }
        }

        private void ClientAuthorityUpdate()
        {
            if (TimeToUpdate && (HasMoved() || HasRotated()))
            {
                SendMessageToServer();
                ClearNeedsUpdate(clientSyncInterval);
            }
        }

        private void ClientInterpolation()
        {
            if (snapshotBuffer.IsEmpty) { return; }

            // we want to set local time to the estimated time that the server was when it send the snapshot
            double serverTime = NetworkTime.time;
            localTime = serverTime - NetworkTime.rtt / 2;

            // we then subtract clientDelay to handle any jitter

            TransformState state = snapshotBuffer.GetLinearInterpolation(localTime - clientDelay);
            Position = state.position;
            Rotation = state.rotation;

            snapshotBuffer.RemoveOldSnapshots((float)(localTime - snapshotRemoveTime), minimumSnapsots);
        }



        [Command]
        public void CmdClientAuthoritySync(TransformState state, double time)
        {
            if (isClient) // host
            {
                ApplyOnClient(state, time);
            }
            else
            {
                ApplyOnServer(state);
            }
        }

        [ClientRpc]
        public void RpcServerSync(TransformState state, double time)
        {
            if (!isServer) // not host
            {
                ApplyOnClient(state, time);
            }
        }
    }
}
