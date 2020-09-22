using UnityEngine;

namespace Mirror.Experimental
{
    [AddComponentMenu("Network/Experimental/NetworkRigidbody")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkRigidbody.html")]
    public class NetworkRigidbody : NetworkBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkRigidbody));

        [Header("Settings")]
        public Rigidbody target;

        [Tooltip("Set to true if moves come from owner client, set to false if moves always come from server")]
        public bool clientAuthority;

        [Header("Velocity")]

        [Tooltip("Syncs Velocity every SyncInterval")]
        public bool syncVelocity = true;

        [Tooltip("Set velocity to 0 each frame (only works if syncVelocity is false")]
        public bool clearVelocity;

        [Tooltip("Only Syncs Value if distance between previous and current is great than sensitivity")]
        public float velocitySensitivity = 0.1f;

        [Header("Angular Velocity")]

        [Tooltip("Syncs AngularVelocity every SyncInterval")]
        public bool syncAngularVelocity = true;

        [Tooltip("Set angularVelocity to 0 each frame (only works if syncAngularVelocity is false")]
        public bool clearAngularVelocity;

        [Tooltip("Only Syncs Value if distance between previous and current is great than sensitivity")]
        public float angularVelocitySensitivity = 0.1f;

        /// <summary>
        /// Values sent on client with authoirty after they are sent to the server
        /// </summary>
        readonly ClientSyncState previousValue = new ClientSyncState();

        void OnValidate()
        {
            if (target == null)
            {
                target = GetComponent<Rigidbody>();
            }
        }

        #region Sync vars
        [SyncVar(hook = nameof(OnVelocityChanged))]
        Vector3 velocity;

        [SyncVar(hook = nameof(OnAngularVelocityChanged))]
        Vector3 angularVelocity;

        [SyncVar(hook = nameof(OnIsKinematicChanged))]
        bool isKinematic;

        [SyncVar(hook = nameof(OnUseGravityChanged))]
        bool useGravity;

        [SyncVar(hook = nameof(OnuDragChanged))]
        float drag;

        [SyncVar(hook = nameof(OnAngularDragChanged))]
        float angularDrag;

        /// <summary>
        /// Ignore value if is host or client with Authority
        /// </summary>
        /// <returns></returns>
        bool IgnoreSync => IsServer || ClientWithAuthority;

        bool ClientWithAuthority => clientAuthority && HasAuthority;

        void OnVelocityChanged(Vector3 _, Vector3 newValue)
        {
            if (IgnoreSync)
                return;

            target.velocity = newValue;
        }


        void OnAngularVelocityChanged(Vector3 _, Vector3 newValue)
        {
            if (IgnoreSync)
                return;

            target.angularVelocity = newValue;
        }

        void OnIsKinematicChanged(bool _, bool newValue)
        {
            if (IgnoreSync)
                return;

            target.isKinematic = newValue;
        }

        void OnUseGravityChanged(bool _, bool newValue)
        {
            if (IgnoreSync)
                return;

            target.useGravity = newValue;
        }

        void OnuDragChanged(float _, float newValue)
        {
            if (IgnoreSync)
                return;

            target.drag = newValue;
        }

        void OnAngularDragChanged(float _, float newValue)
        {
            if (IgnoreSync)
                return;

            target.angularDrag = newValue;
        }
        #endregion


        internal void Update()
        {
            if (IsServer)
            {
                SyncToClients();
            }
            else if (ClientWithAuthority)
            {
                SendToServer();
            }
        }

        internal void FixedUpdate()
        {
            if (clearAngularVelocity && !syncAngularVelocity)
            {
                target.angularVelocity = Vector3.zero;
            }

            if (clearVelocity && !syncVelocity)
            {
                target.velocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Updates sync var values on server so that they sync to the client
        /// </summary>
        [Server]
        void SyncToClients()
        {
            // only update if they have changed more than Sensitivity

            Vector3 currentVelocity = syncVelocity ? target.velocity : default;
            Vector3 currentAngularVelocity = syncAngularVelocity ? target.angularVelocity : default;

            bool velocityChanged = syncVelocity && ((previousValue.velocity - currentVelocity).sqrMagnitude > velocitySensitivity * velocitySensitivity);
            bool angularVelocityChanged = syncAngularVelocity && ((previousValue.angularVelocity - currentAngularVelocity).sqrMagnitude > angularVelocitySensitivity * angularVelocitySensitivity);

            if (velocityChanged)
            {
                velocity = currentVelocity;
                previousValue.velocity = currentVelocity;
            }

            if (angularVelocityChanged)
            {
                angularVelocity = currentAngularVelocity;
                previousValue.angularVelocity = currentAngularVelocity;
            }

            // other rigidbody settings
            isKinematic = target.isKinematic;
            useGravity = target.useGravity;
            drag = target.drag;
            angularDrag = target.angularDrag;
        }

        /// <summary>
        /// Uses ServerRpc to send values to server
        /// </summary>
        [Client]
        void SendToServer()
        {
            if (!HasAuthority)
            {
                logger.LogWarning("SendToServer called without authority");
                return;
            }

            SendVelocity();
            SendRigidBodySettings();
        }

        [Client]
        void SendVelocity()
        {
            float now = Time.time;
            if (now < previousValue.nextSyncTime)
                return;

            Vector3 currentVelocity = syncVelocity ? target.velocity : default;
            Vector3 currentAngularVelocity = syncAngularVelocity ? target.angularVelocity : default;

            bool velocityChanged = syncVelocity && ((previousValue.velocity - currentVelocity).sqrMagnitude > velocitySensitivity * velocitySensitivity);
            bool angularVelocityChanged = syncAngularVelocity && ((previousValue.angularVelocity - currentAngularVelocity).sqrMagnitude > angularVelocitySensitivity * angularVelocitySensitivity);

            // if angularVelocity has changed it is likely that velocity has also changed so just sync both values
            // however if only velocity has changed just send velocity
            if (angularVelocityChanged)
            {
                CmdSendVelocityAndAngular(currentVelocity, currentAngularVelocity);
                previousValue.velocity = currentVelocity;
                previousValue.angularVelocity = currentAngularVelocity;
            }
            else if (velocityChanged)
            {
                CmdSendVelocity(currentVelocity);
                previousValue.velocity = currentVelocity;
            }


            // only update syncTime if either has changed
            if (angularVelocityChanged || velocityChanged)
            {
                previousValue.nextSyncTime = now + syncInterval;
            }
        }

        [Client]
        void SendRigidBodySettings()
        {
            // These shouldn't change often so it is ok to send in their own ServerRpc
            if (previousValue.isKinematic != target.isKinematic)
            {
                CmdSendIsKinematic(target.isKinematic);
                previousValue.isKinematic = target.isKinematic;
            }
            if (previousValue.useGravity != target.useGravity)
            {
                CmdSendUseGravity(target.useGravity);
                previousValue.useGravity = target.useGravity;
            }
            if (previousValue.drag != target.drag)
            {
                CmdSendDrag(target.drag);
                previousValue.drag = target.drag;
            }
            if (previousValue.angularDrag != target.angularDrag)
            {
                CmdSendAngularDrag(target.angularDrag);
                previousValue.angularDrag = target.angularDrag;
            }
        }

        /// <summary>
        /// Called when only Velocity has changed on the client
        /// </summary>
        [ServerRpc]
        void CmdSendVelocity(Vector3 velocity)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
                return;

            this.velocity = velocity;
            target.velocity = velocity;
        }

        /// <summary>
        /// Called when angularVelocity has changed on the client
        /// </summary>
        [ServerRpc]
        void CmdSendVelocityAndAngular(Vector3 velocity, Vector3 angularVelocity)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
                return;

            if (syncVelocity)
            {
                this.velocity = velocity;

                target.velocity = velocity;

            }
            this.angularVelocity = angularVelocity;
            target.angularVelocity = angularVelocity;
        }

        [ServerRpc]
        void CmdSendIsKinematic(bool isKinematic)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
                return;

            this.isKinematic = isKinematic;
            target.isKinematic = isKinematic;
        }

        [ServerRpc]
        void CmdSendUseGravity(bool useGravity)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
                return;

            this.useGravity = useGravity;
            target.useGravity = useGravity;
        }

        [ServerRpc]
        void CmdSendDrag(float drag)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
                return;

            this.drag = drag;
            target.drag = drag;
        }

        [ServerRpc]
        void CmdSendAngularDrag(float angularDrag)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
                return;

            this.angularDrag = angularDrag;
            target.angularDrag = angularDrag;
        }

        /// <summary>
        /// holds previously synced values
        /// </summary>
        public class ClientSyncState
        {
            /// <summary>
            /// Next sync time that velocity will be synced, based on syncInterval.
            /// </summary>
            public float nextSyncTime;
            public Vector3 velocity;
            public Vector3 angularVelocity;
            public bool isKinematic;
            public bool useGravity;
            public float drag;
            public float angularDrag;
        }
    }
}
