using System;
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
        [SerializeField] bool clientAuthority = false;

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
        /// <summary>
        /// latest values from client
        /// </summary>
        TransformState _latestState;
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
                GUILayout.Label($"LocalTime: {NetworkTime.time - NetworkTime.rtt / 2}");
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

        #region Server Sync Update
        private void ServerSyncUpdate()
        {
            if (ServerNeedsToSendUpdate())
            {
                SendMessageToClient();
                ClearNeedsUpdate(clientSyncInterval);
            }
        }

        /// <summary>
        /// Checks if object needs syncing to clients
        /// <para>Called on server</para>
        /// </summary>
        /// <returns></returns>
        bool ServerNeedsToSendUpdate()
        {
            if (IsControlledByServer)
            {
                return TimeToUpdate && (HasMoved() || HasRotated());
            }
            else
            {
                // dont care about time here, if client authority has sent snapshot then always relay it to other clients
                // todo do we need a check for attackers sending too many snapshots?
                return _needsUpdate;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SendMessageToClient()
        {
            // if client has send update, then we want to use the state it gave us
            // this is to make sure we are not sending host's interoplations position as the snapshot insteading sending the client auth snapshot
            TransformState state = IsControlledByServer ? State : _latestState;
            // todo is this correct time?
            RpcServerSync(state, NetworkTime.time);
        }

        [ClientRpc]
        public void RpcServerSync(TransformState state, double time)
        {
            // not host
            // host will have already handled movement in servers code
            if (isServer)
                return;

            ApplyOnClient(state, time);
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
        #endregion


        #region Client Sync Update 
        private void ClientAuthorityUpdate()
        {
            if (TimeToUpdate && (HasMoved() || HasRotated()))
            {
                SendMessageToServer();
                ClearNeedsUpdate(clientSyncInterval);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SendMessageToServer()
        {
            // todo, is this the correct time?
            CmdClientAuthoritySync(State, NetworkTime.time);
        }

        [Command]
        public void CmdClientAuthoritySync(TransformState state, double time)
        {
            // this should not happen, Exception to disconnect attacker
            if (!clientAuthority) { throw new Exception("Client is not allowed to send updated when clientAuthority is false"); }

            _latestState = state;

            // if host apply using interoplation otherwise apply exact 
            if (isClient)
            {
                ApplyOnClient(state, time);
            }
            else
            {
                ApplyOnServer(state);
            }
        }
        #endregion

        #region Client Interpolation
        private void ClientInterpolation()
        {
            if (snapshotBuffer.IsEmpty) { return; }

            // we want to set local time to the estimated time that the server was when it send the snapshot
            double serverTime = NetworkTime.time;
            localTime = serverTime - NetworkTime.rtt / 2;

            // we then subtract clientDelay to handle any jitter

            TransformState state = snapshotBuffer.GetLinearInterpolation(localTime - clientDelay);
            if (logger.LogEnabled()) { logger.Log($"p1:{Position.x} p2:{state.position.x} delta:{Position.x - state.position.x}"); }
            Position = state.position;
            Rotation = state.rotation;

            snapshotBuffer.RemoveOldSnapshots((float)(localTime - snapshotRemoveTime), minimumSnapsots);
        }
        #endregion



    }
}
