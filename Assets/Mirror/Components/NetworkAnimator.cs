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
    /// <para>The NetworkAnimator synchronizes all animation parameters of the selected Animator. It does not automatically synchronize triggers. The function SetTrigger can by used by an object with authority to fire an animation trigger on other clients.</para>
    /// </remarks>
    [AddComponentMenu("Network/Network Animator")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-animator")]
    public class NetworkAnimator : NetworkBehaviour
    {
        [Header("Authority")]
        [Tooltip("Set to true if animations come from owner client,  set to false if animations always come from server")]
        public bool clientAuthority;

        /// <summary>
        /// The animator component to synchronize.
        /// </summary>
        [FormerlySerializedAs("m_Animator")]
        [Header("Animator")]
        [Tooltip("Animator that will have parameters synchronized")]
        public Animator animator;

        /// <summary>
        /// Syncs animator.speed
        /// </summary>
        [SyncVar(hook = nameof(OnAnimatorSpeedChanged))]
        float animatorSpeed;
        float previousSpeed;

        // Note: not an object[] array because otherwise initialization is real annoying
        int[] lastIntParameters;
        float[] lastFloatParameters;
        bool[] lastBoolParameters;
        AnimatorControllerParameter[] parameters;

        // multiple layers
        int[] animationHash;
        int[] transitionHash;
        float[] layerWeight;
        double nextSendTime;

        bool SendMessagesAllowed
        {
            get
            {
                if (isServer)
                {
                    if (!clientAuthority)
                        return true;

                    // This is a special case where we have client authority but we have not assigned the client who has
                    // authority over it, no animator data will be sent over the network by the server.
                    //
                    // So we check here for a connectionToClient and if it is null we will
                    // let the server send animation data until we receive an owner.
                    if (netIdentity != null && netIdentity.connectionToClient == null)
                        return true;
                }

                return (hasAuthority && clientAuthority);
            }
        }

        void Awake()
        {
            // store the animator parameters in a variable - the "Animator.parameters" getter allocates
            // a new parameter array every time it is accessed so we should avoid doing it in a loop
            parameters = animator.parameters
                .Where(par => !animator.IsParameterControlledByCurve(par.nameHash))
                .ToArray();
            lastIntParameters = new int[parameters.Length];
            lastFloatParameters = new float[parameters.Length];
            lastBoolParameters = new bool[parameters.Length];

            animationHash = new int[animator.layerCount];
            transitionHash = new int[animator.layerCount];
            layerWeight = new float[animator.layerCount];
        }

        void FixedUpdate()
        {
            if (!SendMessagesAllowed)
                return;

            if (!animator.enabled)
                return;

            CheckSendRate();

            for (int i = 0; i < animator.layerCount; i++)
            {
                int stateHash;
                float normalizedTime;
                if (!CheckAnimStateChanged(out stateHash, out normalizedTime, i))
                {
                    continue;
                }

                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    WriteParameters(writer);
                    SendAnimationMessage(stateHash, normalizedTime, i, layerWeight[i], writer.ToArray());
                }
            }

            CheckSpeed();
        }

        void CheckSpeed()
        {
            float newSpeed = animator.speed;
            if (Mathf.Abs(previousSpeed - newSpeed) > 0.001f)
            {
                previousSpeed = newSpeed;
                if (isServer)
                {
                    animatorSpeed = newSpeed;
                }
                else if (isClient)
                {
                    CmdSetAnimatorSpeed(newSpeed);
                }
            }
        }

        void OnAnimatorSpeedChanged(float _, float value)
        {
            // skip if host or client with authority
            // they will have already set the speed so don't set again
            if (isServer || (hasAuthority && clientAuthority))
                return;

            animator.speed = value;
        }

        bool CheckAnimStateChanged(out int stateHash, out float normalizedTime, int layerId)
        {
            bool change = false;
            stateHash = 0;
            normalizedTime = 0;

            float lw = animator.GetLayerWeight(layerId);
            if (Mathf.Abs(lw - layerWeight[layerId]) > 0.001f)
            {
                layerWeight[layerId] = lw;
                change = true;
            }

            if (animator.IsInTransition(layerId))
            {
                AnimatorTransitionInfo tt = animator.GetAnimatorTransitionInfo(layerId);
                if (tt.fullPathHash != transitionHash[layerId])
                {
                    // first time in this transition
                    transitionHash[layerId] = tt.fullPathHash;
                    animationHash[layerId] = 0;
                    return true;
                }
                return change;
            }

            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(layerId);
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
            return change;
        }

        void CheckSendRate()
        {
            double now = NetworkTime.localTime;
            if (SendMessagesAllowed && syncInterval >= 0 && now > nextSendTime)
            {
                nextSendTime = now + syncInterval;

                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    if (WriteParameters(writer))
                        SendAnimationParametersMessage(writer.ToArray());
                }
            }
        }

        void SendAnimationMessage(int stateHash, float normalizedTime, int layerId, float weight, byte[] parameters)
        {
            if (isServer)
            {
                RpcOnAnimationClientMessage(stateHash, normalizedTime, layerId, weight, parameters);
            }
            else if (isClient)
            {
                CmdOnAnimationServerMessage(stateHash, normalizedTime, layerId, weight, parameters);
            }
        }

        void SendAnimationParametersMessage(byte[] parameters)
        {
            if (isServer)
            {
                RpcOnAnimationParametersClientMessage(parameters);
            }
            else if (isClient)
            {
                CmdOnAnimationParametersServerMessage(parameters);
            }
        }

        void HandleAnimMsg(int stateHash, float normalizedTime, int layerId, float weight, NetworkReader reader)
        {
            if (hasAuthority && clientAuthority)
                return;

            // usually transitions will be triggered by parameters, if not, play anims directly.
            // NOTE: this plays "animations", not transitions, so any transitions will be skipped.
            // NOTE: there is no API to play a transition(?)
            if (stateHash != 0 && animator.enabled)
            {
                animator.Play(stateHash, layerId, normalizedTime);
            }

            animator.SetLayerWeight(layerId, weight);

            ReadParameters(reader);
        }

        void HandleAnimParamsMsg(NetworkReader reader)
        {
            if (hasAuthority && clientAuthority)
                return;

            ReadParameters(reader);
        }

        void HandleAnimTriggerMsg(int hash)
        {
            if (animator.enabled)
                animator.SetTrigger(hash);
        }

        void HandleAnimResetTriggerMsg(int hash)
        {
            if (animator.enabled)
                animator.ResetTrigger(hash);
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
                    int newIntValue = animator.GetInteger(par.nameHash);
                    changed = newIntValue != lastIntParameters[i];
                    if (changed)
                        lastIntParameters[i] = newIntValue;
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = animator.GetFloat(par.nameHash);
                    changed = Mathf.Abs(newFloatValue - lastFloatParameters[i]) > 0.001f;
                    // only set lastValue if it was changed, otherwise value could slowly drift within the 0.001f limit each frame
                    if (changed)
                        lastFloatParameters[i] = newFloatValue;
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = animator.GetBool(par.nameHash);
                    changed = newBoolValue != lastBoolParameters[i];
                    if (changed)
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
            writer.WriteULong(dirtyBits);
            for (int i = 0; i < parameters.Length; i++)
            {
                if ((dirtyBits & (1ul << i)) == 0)
                    continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newIntValue = animator.GetInteger(par.nameHash);
                    writer.WriteInt(newIntValue);
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = animator.GetFloat(par.nameHash);
                    writer.WriteFloat(newFloatValue);
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = animator.GetBool(par.nameHash);
                    writer.WriteBool(newBoolValue);
                }
            }
            return dirtyBits != 0;
        }

        void ReadParameters(NetworkReader reader)
        {
            bool animatorEnabled = animator.enabled;
            // need to read values from NetworkReader even if animator is disabled

            ulong dirtyBits = reader.ReadULong();
            for (int i = 0; i < parameters.Length; i++)
            {
                if ((dirtyBits & (1ul << i)) == 0)
                    continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newIntValue = reader.ReadInt();
                    if (animatorEnabled)
                        animator.SetInteger(par.nameHash, newIntValue);
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = reader.ReadFloat();
                    if (animatorEnabled)
                        animator.SetFloat(par.nameHash, newFloatValue);
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = reader.ReadBool();
                    if (animatorEnabled)
                        animator.SetBool(par.nameHash, newBoolValue);
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
            bool changed = base.OnSerialize(writer, initialState);
            if (initialState)
            {
                for (int i = 0; i < animator.layerCount; i++)
                {
                    if (animator.IsInTransition(i))
                    {
                        AnimatorStateInfo st = animator.GetNextAnimatorStateInfo(i);
                        writer.WriteInt(st.fullPathHash);
                        writer.WriteFloat(st.normalizedTime);
                    }
                    else
                    {
                        AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(i);
                        writer.WriteInt(st.fullPathHash);
                        writer.WriteFloat(st.normalizedTime);
                    }
                    writer.WriteFloat(animator.GetLayerWeight(i));
                }
                WriteParameters(writer, initialState);
                return true;
            }
            return changed;
        }

        /// <summary>
        /// Custom Deserialization
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="initialState"></param>
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            base.OnDeserialize(reader, initialState);
            if (initialState)
            {
                for (int i = 0; i < animator.layerCount; i++)
                {
                    int stateHash = reader.ReadInt();
                    float normalizedTime = reader.ReadFloat();
                    animator.SetLayerWeight(i, reader.ReadFloat());
                    animator.Play(stateHash, i, normalizedTime);
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
            if (clientAuthority)
            {
                if (!isClient)
                {
                    Debug.LogWarning("Tried to set animation in the server for a client-controlled animator");
                    return;
                }

                if (!hasAuthority)
                {
                    Debug.LogWarning("Only the client with authority can set animations");
                    return;
                }

                if (isClient)
                    CmdOnAnimationTriggerServerMessage(hash);

                // call on client right away
                HandleAnimTriggerMsg(hash);
            }
            else
            {
                if (!isServer)
                {
                    Debug.LogWarning("Tried to set animation in the client for a server-controlled animator");
                    return;
                }

                HandleAnimTriggerMsg(hash);
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
            if (clientAuthority)
            {
                if (!isClient)
                {
                    Debug.LogWarning("Tried to reset animation in the server for a client-controlled animator");
                    return;
                }

                if (!hasAuthority)
                {
                    Debug.LogWarning("Only the client with authority can reset animations");
                    return;
                }

                if (isClient)
                    CmdOnAnimationResetTriggerServerMessage(hash);

                // call on client right away
                HandleAnimResetTriggerMsg(hash);
            }
            else
            {
                if (!isServer)
                {
                    Debug.LogWarning("Tried to reset animation in the client for a server-controlled animator");
                    return;
                }

                HandleAnimResetTriggerMsg(hash);
                RpcOnAnimationResetTriggerClientMessage(hash);
            }
        }

        #region server message handlers

        [Command]
        void CmdOnAnimationServerMessage(int stateHash, float normalizedTime, int layerId, float weight, byte[] parameters)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
                return;

            //Debug.Log($"OnAnimationMessage for netId {netId}");

            // handle and broadcast
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(parameters))
            {
                HandleAnimMsg(stateHash, normalizedTime, layerId, weight, networkReader);
                RpcOnAnimationClientMessage(stateHash, normalizedTime, layerId, weight, parameters);
            }
        }

        [Command]
        void CmdOnAnimationParametersServerMessage(byte[] parameters)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
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
            if (!clientAuthority)
                return;

            // handle and broadcast
            // host should have already the trigger
            bool isHostOwner = isClient && hasAuthority;
            if (!isHostOwner)
            {
                HandleAnimTriggerMsg(hash);
            }

            RpcOnAnimationTriggerClientMessage(hash);
        }

        [Command]
        void CmdOnAnimationResetTriggerServerMessage(int hash)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
                return;

            // handle and broadcast
            // host should have already the trigger
            bool isHostOwner = isClient && hasAuthority;
            if (!isHostOwner)
            {
                HandleAnimResetTriggerMsg(hash);
            }

            RpcOnAnimationResetTriggerClientMessage(hash);
        }

        [Command]
        void CmdSetAnimatorSpeed(float newSpeed)
        {
            // set animator
            animator.speed = newSpeed;
            animatorSpeed = newSpeed;
        }

        #endregion

        #region client message handlers

        [ClientRpc]
        void RpcOnAnimationClientMessage(int stateHash, float normalizedTime, int layerId, float weight, byte[] parameters)
        {
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(parameters))
                HandleAnimMsg(stateHash, normalizedTime, layerId, weight, networkReader);
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
            // host/owner handles this before it is sent
            if (isServer || (clientAuthority && hasAuthority)) return;

            HandleAnimTriggerMsg(hash);
        }

        [ClientRpc]
        void RpcOnAnimationResetTriggerClientMessage(int hash)
        {
            // host/owner handles this before it is sent
            if (isServer || (clientAuthority && hasAuthority)) return;

            HandleAnimResetTriggerMsg(hash);
        }

        #endregion
    }
}
