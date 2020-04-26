using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror
{
    /// <summary>
    /// A component to synchronize Mecanim animation states for networked objects.
    /// </summary>
    /// <remarks>
    /// <para>The animation of game objects can be networked by this component. There are two models of authority for networked movement:</para>
    /// <para>If the object has authority on the client, then it should be animated locally on the owning client. The animation state information will be sent from the owning client to the server, then broadcast to all of the other clients. This is common for player objects.</para>
    /// <para>If the object has authority on the server, then it should be animated on the server and state information will be sent to all clients. This is common for objects not related to a specific client, such as an enemy unit.</para>
    /// <para>The NetworkAnimator synchronizes all animation parameters of the selected Animator. It does not automatically sychronize triggers. The function SetTrigger can by used by an object with authority to fire an animation trigger on other clients.</para>
    /// </remarks>
    [AddComponentMenu("Network/NetworkAnimator")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkAnimator.html")]
    public class NetworkAnimator : NetworkBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkAnimator));

        [Header("Authority")]
        [Tooltip("Set to true if animations come from owner client,  set to false if animations always come from server")]
        public bool ClientAuthority;

        /// <summary>
        /// The animator component to synchronize.
        /// </summary>
        [FormerlySerializedAs("m_Animator")]
        [Header("Animator")]
        [Tooltip("Animator that will have parameters synchronized")]
        public Animator Animator;

        // Note: not an object[] array because otherwise initialization is real annoying
        int[] lastIntParameters;
        float[] lastFloatParameters;
        bool[] lastBoolParameters;
        AnimatorControllerParameter[] parameters;

        // multiple layers
        int[] animationHash;
        int[] transitionHash;
        float sendTimer;

        bool SendMessagesAllowed
        {
            get
            {
                if (IsServer)
                {
                    if (!ClientAuthority)
                        return true;

                    // This is a special case where we have client authority but we have not assigned the client who has
                    // authority over it, no animator data will be sent over the network by the server.
                    //
                    // So we check here for a connectionToClient and if it is null we will
                    // let the server send animation data until we receive an owner.
                    if (NetIdentity != null && NetIdentity.ConnectionToClient == null)
                        return true;
                }

                return HasAuthority && ClientAuthority;
            }
        }

        void Awake()
        {
            // store the animator parameters in a variable - the "Animator.parameters" getter allocates
            // a new parameter array every time it is accessed so we should avoid doing it in a loop
            parameters = Animator.parameters
                .Where(par => !Animator.IsParameterControlledByCurve(par.nameHash))
                .ToArray();
            lastIntParameters = new int[parameters.Length];
            lastFloatParameters = new float[parameters.Length];
            lastBoolParameters = new bool[parameters.Length];

            animationHash = new int[Animator.layerCount];
            transitionHash = new int[Animator.layerCount];
        }

        void FixedUpdate()
        {
            if (!SendMessagesAllowed)
                return;

            CheckSendRate();

            for (int i = 0; i < Animator.layerCount; i++)
            {
                if (!CheckAnimStateChanged(out int stateHash, out float normalizedTime, i))
                {
                    continue;
                }

                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    WriteParameters(writer);
                    SendAnimationMessage(stateHash, normalizedTime, i, writer.ToArray());
                }
            }
        }

        bool CheckAnimStateChanged(out int stateHash, out float normalizedTime, int layerId)
        {
            stateHash = 0;
            normalizedTime = 0;

            if (Animator.IsInTransition(layerId))
            {
                AnimatorTransitionInfo tt = Animator.GetAnimatorTransitionInfo(layerId);
                if (tt.fullPathHash != transitionHash[layerId])
                {
                    // first time in this transition
                    transitionHash[layerId] = tt.fullPathHash;
                    animationHash[layerId] = 0;
                    return true;
                }
                return false;
            }

            AnimatorStateInfo st = Animator.GetCurrentAnimatorStateInfo(layerId);
            if (st.fullPathHash != animationHash[layerId])
            {
                // first time in this animation state
                if (animationHash[layerId] != 0)
                {
                    // came from another animation directly - from Play()
                    stateHash = st.fullPathHash;
                    normalizedTime = st.normalizedTime;
                }
                transitionHash[layerId] = 0;
                animationHash[layerId] = st.fullPathHash;
                return true;
            }
            return false;
        }

        void CheckSendRate()
        {
            if (SendMessagesAllowed && syncInterval > 0 && sendTimer < Time.time)
            {
                sendTimer = Time.time + syncInterval;

                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    if (WriteParameters(writer))
                        SendAnimationParametersMessage(writer.ToArray());
                }
            }
        }

        void SendAnimationMessage(int stateHash, float normalizedTime, int layerId, byte[] parameters)
        {
            if (IsServer)
            {
                RpcOnAnimationClientMessage(stateHash, normalizedTime, layerId, parameters);
            }
            else if (Client.Connection != null)
            {
                CmdOnAnimationServerMessage(stateHash, normalizedTime, layerId, parameters);
            }
        }

        void SendAnimationParametersMessage(byte[] parameters)
        {
            if (IsServer)
            {
                RpcOnAnimationParametersClientMessage(parameters);
            }
            else if (Client.Connection != null)
            {
                CmdOnAnimationParametersServerMessage(parameters);
            }
        }

        void HandleAnimMsg(int stateHash, float normalizedTime, int layerId, NetworkReader reader)
        {
            if (HasAuthority && ClientAuthority)
                return;

            // usually transitions will be triggered by parameters, if not, play anims directly.
            // NOTE: this plays "animations", not transitions, so any transitions will be skipped.
            // NOTE: there is no API to play a transition(?)
            if (stateHash != 0)
            {
                Animator.Play(stateHash, layerId, normalizedTime);
            }

            ReadParameters(reader);
        }

        void HandleAnimParamsMsg(NetworkReader reader)
        {
            if (HasAuthority && ClientAuthority)
                return;

            ReadParameters(reader);
        }

        void HandleAnimTriggerMsg(int hash)
        {
            Animator.SetTrigger(hash);
        }

        void HandleAnimResetTriggerMsg(int hash)
        {
            Animator.ResetTrigger(hash);
        }

        ulong NextDirtyBits()
        {
            ulong dirtyBits = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter par = parameters[i];
                bool changed = false;
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newIntValue = Animator.GetInteger(par.nameHash);
                    changed = newIntValue != lastIntParameters[i];
                    lastIntParameters[i] = newIntValue;
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = Animator.GetFloat(par.nameHash);
                    changed = Mathf.Abs(newFloatValue - lastFloatParameters[i]) > 0.001f;
                    lastFloatParameters[i] = newFloatValue;
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = Animator.GetBool(par.nameHash);
                    changed = newBoolValue != lastBoolParameters[i];
                    lastBoolParameters[i] = newBoolValue;
                }
                if (changed)
                {
                    dirtyBits |= 1ul << i;
                }
            }
            return dirtyBits;
        }

        bool WriteParameters(NetworkWriter writer, bool forceAll = false)
        {
            ulong dirtyBits = forceAll ? (~0ul) : NextDirtyBits();
            writer.WritePackedUInt64(dirtyBits);
            for (int i = 0; i < parameters.Length; i++)
            {
                if ((dirtyBits & (1ul << i)) == 0)
                    continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newIntValue = Animator.GetInteger(par.nameHash);
                    writer.WritePackedInt32(newIntValue);
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = Animator.GetFloat(par.nameHash);
                    writer.WriteSingle(newFloatValue);
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = Animator.GetBool(par.nameHash);
                    writer.WriteBoolean(newBoolValue);
                }
            }
            return dirtyBits != 0;
        }

        void ReadParameters(NetworkReader reader)
        {
            ulong dirtyBits = reader.ReadPackedUInt64();
            for (int i = 0; i < parameters.Length; i++)
            {
                if ((dirtyBits & (1ul << i)) == 0)
                    continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newIntValue = reader.ReadPackedInt32();
                    Animator.SetInteger(par.nameHash, newIntValue);
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = reader.ReadSingle();
                    Animator.SetFloat(par.nameHash, newFloatValue);
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = reader.ReadBoolean();
                    Animator.SetBool(par.nameHash, newBoolValue);
                }
            }
        }

        /// <summary>
        /// Custom Serialization
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="initialState"></param>
        /// <returns></returns>
        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (initialState)
            {
                for (int i = 0; i < Animator.layerCount; i++)
                {
                    if (Animator.IsInTransition(i))
                    {
                        AnimatorStateInfo st = Animator.GetNextAnimatorStateInfo(i);
                        writer.WriteInt32(st.fullPathHash);
                        writer.WriteSingle(st.normalizedTime);
                    }
                    else
                    {
                        AnimatorStateInfo st = Animator.GetCurrentAnimatorStateInfo(i);
                        writer.WriteInt32(st.fullPathHash);
                        writer.WriteSingle(st.normalizedTime);
                    }
                }
                WriteParameters(writer, initialState);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Custom Deserialization
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="initialState"></param>
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                for (int i = 0; i < Animator.layerCount; i++)
                {
                    int stateHash = reader.ReadInt32();
                    float normalizedTime = reader.ReadSingle();
                    Animator.Play(stateHash, i, normalizedTime);
                }

                ReadParameters(reader);
            }
        }

        /// <summary>
        /// Causes an animation trigger to be invoked for a networked object.
        /// <para>If local authority is set, and this is called from the client, then the trigger will be invoked on the server and all clients. If not, then this is called on the server, and the trigger will be called on all clients.</para>
        /// </summary>
        /// <param name="triggerName">Name of trigger.</param>
        public void SetTrigger(string triggerName)
        {
            SetTrigger(Animator.StringToHash(triggerName));
        }

        /// <summary>
        /// Causes an animation trigger to be invoked for a networked object.
        /// </summary>
        /// <param name="hash">Hash id of trigger (from the Animator).</param>
        public void SetTrigger(int hash)
        {
            if (ClientAuthority)
            {
                if (!IsClient)
                {
                    logger.LogWarning("Tried to set animation in the server for a client-controlled animator");
                    return;
                }

                if (!HasAuthority)
                {
                    logger.LogWarning("Only the client with authority can set animations");
                    return;
                }

                if (Client.Connection != null)
                    CmdOnAnimationTriggerServerMessage(hash);
            }
            else
            {
                if (!IsServer)
                {
                    logger.LogWarning("Tried to set animation in the client for a server-controlled animator");
                    return;
                }

                RpcOnAnimationTriggerClientMessage(hash);
            }
        }

        /// <summary>
        /// Causes an animation trigger to be reset for a networked object.
        /// <para>If local authority is set, and this is called from the client, then the trigger will be reset on the server and all clients. If not, then this is called on the server, and the trigger will be reset on all clients.</para>
        /// </summary>
        /// <param name="triggerName">Name of trigger.</param>
        public void ResetTrigger(string triggerName)
        {
            ResetTrigger(Animator.StringToHash(triggerName));
        }

        /// <summary>
        /// Causes an animation trigger to be reset for a networked object.
        /// </summary>
        /// <param name="hash">Hash id of trigger (from the Animator).</param>
        public void ResetTrigger(int hash)
        {
            if (ClientAuthority)
            {
                if (!IsClient)
                {
                    logger.LogWarning("Tried to reset animation in the server for a client-controlled animator");
                    return;
                }

                if (!HasAuthority)
                {
                    logger.LogWarning("Only the client with authority can reset animations");
                    return;
                }

                if (Client.Connection != null)
                    CmdOnAnimationResetTriggerServerMessage(hash);
            }
            else
            {
                if (!IsServer)
                {
                    logger.LogWarning("Tried to reset animation in the client for a server-controlled animator");
                    return;
                }

                RpcOnAnimationResetTriggerClientMessage(hash);
            }
        }

        #region server message handlers

        [Command]
        void CmdOnAnimationServerMessage(int stateHash, float normalizedTime, int layerId, byte[] parameters)
        {
            // Ignore messages from client if not in client authority mode
            if (!ClientAuthority)
                return;

            if (logger.LogEnabled()) logger.Log("OnAnimationMessage for netId=" + NetId);

            // handle and broadcast
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(parameters))
            {
                HandleAnimMsg(stateHash, normalizedTime, layerId, networkReader);
                RpcOnAnimationClientMessage(stateHash, normalizedTime, layerId, parameters);
            }
        }

        [Command]
        void CmdOnAnimationParametersServerMessage(byte[] parameters)
        {
            // Ignore messages from client if not in client authority mode
            if (!ClientAuthority)
                return;

            // handle and broadcast
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(parameters))
            {
                HandleAnimParamsMsg(networkReader);
                RpcOnAnimationParametersClientMessage(parameters);
            }
        }

        [Command]
        void CmdOnAnimationTriggerServerMessage(int hash)
        {
            // Ignore messages from client if not in client authority mode
            if (!ClientAuthority)
                return;

            // handle and broadcast
            HandleAnimTriggerMsg(hash);
            RpcOnAnimationTriggerClientMessage(hash);
        }

        [Command]
        void CmdOnAnimationResetTriggerServerMessage(int hash)
        {
            // Ignore messages from client if not in client authority mode
            if (!ClientAuthority)
                return;

            // handle and broadcast
            HandleAnimResetTriggerMsg(hash);
            RpcOnAnimationResetTriggerClientMessage(hash);
        }

        #endregion

        #region client message handlers

        [ClientRpc]
        void RpcOnAnimationClientMessage(int stateHash, float normalizedTime, int layerId, byte[] parameters)
        {
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(parameters))
                HandleAnimMsg(stateHash, normalizedTime, layerId, networkReader);
        }

        [ClientRpc]
        void RpcOnAnimationParametersClientMessage(byte[] parameters)
        {
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(parameters))
                HandleAnimParamsMsg(networkReader);
        }

        [ClientRpc]
        void RpcOnAnimationTriggerClientMessage(int hash)
        {
            if (IsServer) return;

            HandleAnimTriggerMsg(hash);
        }

        [ClientRpc]
        void RpcOnAnimationResetTriggerClientMessage(int hash)
        {
            if (IsServer) return;

            HandleAnimResetTriggerMsg(hash);
        }

        #endregion
    }
}
