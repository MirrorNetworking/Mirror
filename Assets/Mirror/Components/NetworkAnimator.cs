using System;
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
    [HelpURL("https://mirror-networking.com/docs/Articles/Components/NetworkAnimator.html")]
    public class NetworkAnimator : NetworkBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkAnimator));

        [Header("Authority")]
        [Tooltip("Set to true if animations come from owner client,  set to false if animations always come from server")]
        public bool clientAuthority;

        /// <summary>
        /// The animator component to synchronize.
        /// </summary>
        [FormerlySerializedAs("m_Animator")]
        [FormerlySerializedAs("animator")]
        [Header("Animator")]
        [Tooltip("Animator that will have parameters synchronized")]
        [SerializeField] Animator _animator = null;


        /// <summary>
        /// Syncs animator.speed
        /// </summary>
        [SyncVar(hook = nameof(onAnimatorSpeedChanged))]
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
        float nextSendTime;

        public bool HasAnimator => _animator != null;
        public bool HasEnabledAnimator => _animator != null && _animator.enabled;

        bool FieldsAreInitialized => parameters != null;

        bool hasSetup;
        // cache message if Animator not set
        byte[] setupMsg;

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

        public Animator animator
        {
            get => _animator;
            set
            {
                // Changing animator after one is already set can cause problems 
                // if a new set up message is received before the new animator is set on the client then the values would be applied to the wrong animator
                // the user would have to find a way to synchronize setting the animator to the right animator on both the client and server
                if (HasAnimator)
                {
                    logger.LogError("setting animator when one has already been set");
                    return;
                }

                if (value == null)
                {
                    logger.LogError("animator should not be set to null");
                    return;
                }

                _animator = value;

                // is not server or client yet dont worry, OnStartServer will run the setup
                if (isServer)
                {
                    SetupServer();
                    // if this is called before OnStartServer it is fine because there will be no observer
                    // if called after OnStartServer then it will send to existing client that dont have the intial setup from desialize
                    SendSetupToObservers();
                }
                else if (isClient)
                {
                    SetupClient();
                }
            }
        }


        public override void OnStartServer()
        {
            if (HasAnimator)
            {
                SetupServer();
            }
        }

        /// <summary>
        /// Called on Start or when Animator is first set
        /// </summary>
        void SetupServer()
        {
            logger.Assert(HasAnimator, "SetupServer called before animator set");

            // only set up fields once
            if (!FieldsAreInitialized)
            {
                SetupArrayFields();
            }
        }

        /// <summary>
        /// Called when animator is first set
        /// </summary>
        void SetupClient()
        {
            logger.Assert(HasAnimator, "SetupClient called before animator set");

            // if spawn message set setup now
            if (setupMsg != null)
            {
                ApplyInitialState(new ArraySegment<byte>(setupMsg));
                // clear setup message after setup
                setupMsg = null;
            }
        }

        void SetupArrayFields()
        {
            if (FieldsAreInitialized)
            {
                logger.LogWarning("Trying to set up Array fields more than once");
                return;
            }

            logger.Assert(animator.enabled, "Setup called when animator was disabled");

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
            if (!hasSetup || !SendMessagesAllowed)
                return;

            if (!HasEnabledAnimator)
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
                else if (ClientScene.readyConnection != null)
                {
                    CmdSetAnimatorSpeed(newSpeed);
                }
            }
        }

        void CmdSetAnimatorSpeed(float newSpeed)
        {
            // set animator
            animator.speed = newSpeed;
            animatorSpeed = newSpeed;
        }

        void onAnimatorSpeedChanged(float _, float value)
        {
            // skip if host or client with authoity
            // they will have already set the speed so dont set again
            if (isServer || (hasAuthority && clientAuthority))
                return;

            animator.speed = value;
        }

        bool CheckAnimStateChanged(out int stateHash, out float normalizedTime, int layerId)
        {
            bool change = false;
            logger.Assert(HasAnimator, "CheckAnimStateChanged called before animator set");

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
            float now = Time.time;
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
            else if (ClientScene.readyConnection != null)
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
            else if (ClientScene.readyConnection != null)
            {
                CmdOnAnimationParametersServerMessage(parameters);
            }
        }

        void HandleAnimMsg(int stateHash, float normalizedTime, int layerId, float weight, NetworkReader reader)
        {
            if (hasAuthority && clientAuthority)
                return;
            logger.Assert(HasAnimator, "HandleAnimMsg called before animator set");

            // usually transitions will be triggered by parameters, if not, play anims directly.
            // NOTE: this plays "animations", not transitions, so any transitions will be skipped.
            // NOTE: there is no API to play a transition(?)
            if (stateHash != 0 && HasEnabledAnimator)
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
            logger.Assert(HasAnimator, "HandleAnimParamsMsg called before animator set");

            ReadParameters(reader);
        }

        void HandleAnimTriggerMsg(int hash)
        {
            if (HasEnabledAnimator)
            {
                animator.SetTrigger(hash);
            }
        }

        void HandleAnimResetTriggerMsg(int hash)
        {
            if (HasEnabledAnimator)
            {
                animator.ResetTrigger(hash);
            }
        }

        ulong NextDirtyBits()
        {
            logger.Assert(HasAnimator, "NextDirtyBits called before animator set");

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
            logger.Assert(HasAnimator, "WriteParameters called before animator set");

            ulong dirtyBits = forceAll ? (~0ul) : NextDirtyBits();
            writer.WriteUInt64(dirtyBits);
            for (int i = 0; i < parameters.Length; i++)
            {
                if ((dirtyBits & (1ul << i)) == 0)
                    continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newIntValue = animator.GetInteger(par.nameHash);
                    writer.WriteInt32(newIntValue);
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = animator.GetFloat(par.nameHash);
                    writer.WriteSingle(newFloatValue);
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = animator.GetBool(par.nameHash);
                    writer.WriteBoolean(newBoolValue);
                }
            }
            return dirtyBits != 0;
        }

        void ReadParameters(NetworkReader reader)
        {
            logger.Assert(HasAnimator, "ReadParameters called before animator set");

            bool animatorEnabled = HasEnabledAnimator;
            // need to read values from NetworkReader even if animator is disabled

            ulong dirtyBits = reader.ReadUInt64();
            for (int i = 0; i < parameters.Length; i++)
            {
                if ((dirtyBits & (1ul << i)) == 0)
                    continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newIntValue = reader.ReadInt32();
                    if (animatorEnabled)
                        animator.SetInteger(par.nameHash, newIntValue);
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = reader.ReadSingle();
                    if (animatorEnabled)
                        animator.SetFloat(par.nameHash, newFloatValue);
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = reader.ReadBoolean();
                    if (animatorEnabled)
                        animator.SetBool(par.nameHash, newBoolValue);
                }
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
                    logger.LogWarning("Tried to set animation in the server for a client-controlled animator");
                    return;
                }

                if (!hasAuthority)
                {
                    logger.LogWarning("Only the client with authority can set animations");
                    return;
                }

                if (ClientScene.readyConnection != null)
                    CmdOnAnimationTriggerServerMessage(hash);

                // call on client right away
                HandleAnimTriggerMsg(hash);
            }
            else
            {
                if (!isServer)
                {
                    logger.LogWarning("Tried to set animation in the client for a server-controlled animator");
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
                    logger.LogWarning("Tried to reset animation in the server for a client-controlled animator");
                    return;
                }

                if (!hasAuthority)
                {
                    logger.LogWarning("Only the client with authority can reset animations");
                    return;
                }

                if (ClientScene.readyConnection != null)
                    CmdOnAnimationResetTriggerServerMessage(hash);

                // call on client right away
                HandleAnimResetTriggerMsg(hash);
            }
            else
            {
                if (!isServer)
                {
                    logger.LogWarning("Tried to reset animation in the client for a server-controlled animator");
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

            if (logger.LogEnabled()) logger.Log($"OnAnimationMessage for netId={netId}");

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

        #region intial setup
        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            _ = base.OnSerialize(writer, initialState);

            // only write if HasAnimator
            // and initial state (for late joiners, existing clients get send an RPC when Animator is set)
            bool needtToWrite = HasAnimator && initialState;

            // tell clients if they need to read or not
            writer.WriteBoolean(needtToWrite);

            if (needtToWrite)
            {
                using (PooledNetworkWriter innerWriter = NetworkWriterPool.GetWriter())
                {
                    WriteInitialState(innerWriter);
                    // write arraysegment because client might not have HaveAnimator so wont know how much to read
                    ArraySegment<byte> bytes = innerWriter.ToArraySegment();
                    writer.WriteArraySegment(bytes);
                }
            }

            // always writing atleast 1 byte if dirty
            return true;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            base.OnDeserialize(reader, initialState);

            bool needToRead = reader.ReadBoolean();

            if (needToRead)
            {
                logger.Assert(initialState, "Should only be reading if initialState is true");
                // always read so that reader is is correct position for next Component
                ArraySegment<byte> data = reader.ReadBytesAndSizeSegment();
                HandleInitialStateMessage(data);
            }
        }

        [Server]
        void SendSetupToObservers()
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                WriteInitialState(writer);
                // write arraysegment because client might not have HaveAnimator so wont know how much to read
                ArraySegment<byte> bytes = writer.ToArraySegment();
                RpcInitialSetup(bytes);
            }
        }
        [ClientRpc]
        void RpcInitialSetup(ArraySegment<byte> data)
        {
            HandleInitialStateMessage(data);
        }

        [Server]
        void WriteInitialState(NetworkWriter writer)
        {
            // mark ready on server
            hasSetup = true;

            for (int i = 0; i < animator.layerCount; i++)
            {
                if (animator.IsInTransition(i))
                {
                    AnimatorStateInfo st = animator.GetNextAnimatorStateInfo(i);
                    writer.WriteInt32(st.fullPathHash);
                    writer.WriteSingle(st.normalizedTime);
                }
                else
                {
                    AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(i);
                    writer.WriteInt32(st.fullPathHash);
                    writer.WriteSingle(st.normalizedTime);
                }
                writer.WriteSingle(animator.GetLayerWeight(i));
            }
            WriteParameters(writer, true);
        }

        /// <summary>
        /// if animator set then apply payload right away, else store it until animator is set locally
        /// </summary>
        /// <param name="data"></param>
        void HandleInitialStateMessage(ArraySegment<byte> data)
        {
            // host players doesnt need to be setup
            if (isServer) { return; }

            logger.Assert(!hasSetup, "Setup should not be called mutliple times");

            // if animator has been set, handle the message right way
            if (HasAnimator)
            {
                ApplyInitialState(data);
            }
            // else wait for animator to be set and handle the message in setup
            else
            {
                // copy to array because ArraySegemnt could be re-used and overwritten
                setupMsg = data.ToArray();
            }
        }

        /// <summary>
        /// this method is called when both animator has been set, and setup payload has been recieved
        /// </summary>
        /// <param name="parameters"></param>
        void ApplyInitialState(ArraySegment<byte> parameters)
        {
            logger.Assert(HasAnimator, "HandleAnimSetMsg called before animator set");

            // make sure to set up fields first
            SetupArrayFields();

            using (PooledNetworkReader reader = NetworkReaderPool.GetReader(parameters))
            {
                for (int i = 0; i < animator.layerCount; i++)
                {
                    int stateHash = reader.ReadInt32();
                    float normalizedTime = reader.ReadSingle();
                    animator.SetLayerWeight(i, reader.ReadSingle());
                    animator.Play(stateHash, i, normalizedTime);
                }

                ReadParameters(reader);
            }

            // mark client as setup
            hasSetup = true;
        }
        #endregion
    }
}
