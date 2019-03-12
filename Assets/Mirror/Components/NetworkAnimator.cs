using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkAnimator")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkAnimator")]
    public class NetworkAnimator : NetworkBehaviour
    {
        // configuration
        [SerializeField] Animator m_Animator;
        [SerializeField] uint m_ParameterSendBits;
        // Note: not an object[] array because otherwise initialization is real annoying
        int[] lastIntParameters;
        float[] lastFloatParameters;
        bool[] lastBoolParameters;
        AnimatorControllerParameter[] parameters;

#if UNITY_EDITOR
        // Editor-only, this should never be changed at runtime.
        public Animator animator
        {
            get => m_Animator;
            set
            {
                m_Animator = value;
                ResetParameterOptions();
            }
        }

        public void SetParameterAutoSend(int index, bool value)
        {
            if (value)
            {
                m_ParameterSendBits |= (uint)(1 << index);
            }
            else
            {
                m_ParameterSendBits &= (uint)(~(1 << index));
            }
        }

        public bool GetParameterAutoSend(int index)
        {
            return (m_ParameterSendBits & (uint)(1 << index)) != 0;
        }

        public void ResetParameterOptions()
        {
            m_ParameterSendBits = 0;
        }
#endif

        void Awake()
        {
            // cache parameter array because the accessor allocates a new array every time
            parameters = m_Animator.parameters;
            lastIntParameters = new int[parameters.Length];
            lastFloatParameters = new float[parameters.Length];
            lastBoolParameters = new bool[parameters.Length];
        }

        int m_AnimationHash;
        int m_TransitionHash;
        float m_SendTimer;

        bool sendMessagesAllowed
        {
            get
            {
                if (isServer)
                {
                    if (!localPlayerAuthority)
                        return true;

                    // This is a special case where we have localPlayerAuthority set
                    // on a NetworkIdentity but we have not assigned the client who has
                    // authority over it, no animator data will be sent over the network by the server.
                    //
                    // So we check here for a clientAuthorityOwner and if it is null we will
                    // let the server send animation data until we receive an owner.
                    if (netIdentity != null && netIdentity.clientAuthorityOwner == null)
                        return true;
                }

                return hasAuthority;
            }
        }

        void FixedUpdate()
        {
            if (!sendMessagesAllowed) return;

            if (CheckAnimStateChanged(out int stateHash, out float normalizedTime))
            {
                // GetDirtyBits() here, not outside the if, because it updates the lastParameter values.
                uint dirtyBits = GetDirtyBits();
                SendAnimationMessage(stateHash, normalizedTime, dirtyBits, WriteParametersArray(dirtyBits));
            }
            else if (CheckSendRate())
            {
                uint dirtyBits = GetDirtyBits();
                SendAnimationParametersMessage(dirtyBits, WriteParametersArray(dirtyBits));
            }
        }

        bool CheckAnimStateChanged(out int stateHash, out float normalizedTime)
        {
            stateHash = 0;
            normalizedTime = 0;

            if (m_Animator.IsInTransition(0))
            {
                AnimatorTransitionInfo tt = m_Animator.GetAnimatorTransitionInfo(0);
                if (tt.fullPathHash != m_TransitionHash)
                {
                    // first time in this transition
                    m_TransitionHash = tt.fullPathHash;
                    m_AnimationHash = 0;
                    return true;
                }
                return false;
            }

            AnimatorStateInfo st = m_Animator.GetCurrentAnimatorStateInfo(0);
            if (st.fullPathHash != m_AnimationHash)
            {
                // first time in this animation state
                if (m_AnimationHash != 0)
                {
                    // came from another animation directly - from Play()
                    stateHash = st.fullPathHash;
                    normalizedTime = st.normalizedTime;
                }
                m_TransitionHash = 0;
                m_AnimationHash = st.fullPathHash;
                return true;
            }
            return false;
        }

        bool CheckSendRate()
        {
            return sendMessagesAllowed && syncInterval != 0 && m_SendTimer < Time.time;
        }

        void ResetSendTime()
        {
            m_SendTimer = Time.time + syncInterval;
        }

        void SendAnimationMessage(int stateHash, float normalizedTime, uint dirtyBits, byte[] parameters)
        {
            ResetSendTime();
            if (isServer)
            {
                RpcOnAnimationClientMessage(stateHash, normalizedTime, dirtyBits, parameters);
            }
            else if (ClientScene.readyConnection != null)
            {
                CmdOnAnimationServerMessage(stateHash, normalizedTime, dirtyBits, parameters);
            }
        }

        void SendAnimationParametersMessage(uint dirtyBits, byte[] parameters)
        {
            ResetSendTime();
            if (isServer)
            {
                RpcOnAnimationParametersClientMessage(dirtyBits, parameters);
            }
            else if (ClientScene.readyConnection != null)
            {
                CmdOnAnimationParametersServerMessage(dirtyBits, parameters);
            }
        }

        internal void HandleAnimMsg(int stateHash, float normalizedTime, uint dirtyBits, NetworkReader reader)
        {
            if (hasAuthority) return;

            // usually transitions will be triggered by parameters, if not, play anims directly.
            // NOTE: this plays "animations", not transitions, so any transitions will be skipped.
            // NOTE: there is no API to play a transition(?)
            if (stateHash != 0)
            {
                m_Animator.Play(stateHash, 0, normalizedTime);
            }

            ReadParameters(dirtyBits, reader);
        }

        internal void HandleAnimParamsMsg(uint dirtyBits, NetworkReader reader)
        {
            if (hasAuthority) return;

            ReadParameters(dirtyBits, reader);
        }

        internal void HandleAnimTriggerMsg(int hash)
        {
            m_Animator.SetTrigger(hash);
        }

        uint GetDirtyBits()
        {
            uint dirtyBits = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                bool parameterDirty = false;
                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newIntValue = m_Animator.GetInteger(par.nameHash);
                    parameterDirty = newIntValue != lastIntParameters[i];
                    lastIntParameters[i] = newIntValue;
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = m_Animator.GetFloat(par.nameHash);
                    parameterDirty = newFloatValue != lastFloatParameters[i];
                    lastFloatParameters[i] = newFloatValue;
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = m_Animator.GetBool(par.nameHash);
                    parameterDirty = newBoolValue != lastBoolParameters[i];
                    lastBoolParameters[i] = newBoolValue;
                }
                if (parameterDirty) dirtyBits |= 1u << i;
            }
            // Mask the dirty bits by the user specified parameters to send.
            return dirtyBits & m_ParameterSendBits;
        }

        byte[] WriteParametersArray(uint dirtyBits)
        {
            NetworkWriter writer = new NetworkWriter();
            WriteParameters(dirtyBits, writer);
            return writer.ToArray();
        }

        void WriteParameters(uint dirtyBits, NetworkWriter writer)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if ((dirtyBits & (1 << i)) == 0) continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    writer.WritePackedUInt32((uint) m_Animator.GetInteger(par.nameHash));
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    writer.Write(m_Animator.GetFloat(par.nameHash));
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    writer.Write(m_Animator.GetBool(par.nameHash));
                }
            }
        }

        void ReadParameters(uint dirtyBits, NetworkReader reader)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if ((dirtyBits & (1 << i)) == 0) continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    m_Animator.SetInteger(par.nameHash, (int)reader.ReadPackedUInt32());
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    m_Animator.SetFloat(par.nameHash, reader.ReadSingle());
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    m_Animator.SetBool(par.nameHash, reader.ReadBoolean());
                }
            }
        }

        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            if (forceAll)
            {
                if (m_Animator.IsInTransition(0))
                {
                    AnimatorStateInfo st = m_Animator.GetNextAnimatorStateInfo(0);
                    writer.Write(st.fullPathHash);
                    writer.Write(st.normalizedTime);
                }
                else
                {
                    AnimatorStateInfo st = m_Animator.GetCurrentAnimatorStateInfo(0);
                    writer.Write(st.fullPathHash);
                    writer.Write(st.normalizedTime);
                }
                WriteParameters(m_ParameterSendBits, writer);
                return true;
            }
            return false;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                int stateHash = reader.ReadInt32();
                float normalizedTime = reader.ReadSingle();
                ReadParameters(m_ParameterSendBits, reader);
                m_Animator.Play(stateHash, 0, normalizedTime);
            }
        }

        public void SetTrigger(string triggerName)
        {
            SetTrigger(Animator.StringToHash(triggerName));
        }

        public void SetTrigger(int hash)
        {
            if (hasAuthority && localPlayerAuthority)
            {
                if (NetworkClient.singleton != null && ClientScene.readyConnection != null)
                {
                    CmdOnAnimationTriggerServerMessage(hash);
                }
                return;
            }

            if (isServer && !localPlayerAuthority)
            {
                RpcOnAnimationTriggerClientMessage(hash);
            }
        }

        #region server message handlers
        [Command]
        void CmdOnAnimationServerMessage(int stateHash, float normalizedTime, uint dirtyBits, byte[] parameters)
        {
            if (LogFilter.Debug) Debug.Log("OnAnimationMessage for netId=" + netId);

            // handle and broadcast
            HandleAnimMsg(stateHash, normalizedTime, dirtyBits, new NetworkReader(parameters));
            RpcOnAnimationClientMessage(stateHash, normalizedTime, dirtyBits, parameters);
        }

        [Command]
        void CmdOnAnimationParametersServerMessage(uint dirtyBits, byte[] parameters)
        {
            // handle and broadcast
            HandleAnimParamsMsg(dirtyBits, new NetworkReader(parameters));
            RpcOnAnimationParametersClientMessage(dirtyBits, parameters);
        }

        [Command]
        void CmdOnAnimationTriggerServerMessage(int hash)
        {
            // handle and broadcast
            HandleAnimTriggerMsg(hash);
            RpcOnAnimationTriggerClientMessage(hash);
        }
        #endregion

        #region client message handlers
        [ClientRpc]
        void RpcOnAnimationClientMessage(int stateHash, float normalizedTime, uint dirtyBits, byte[] parameters)
        {
            HandleAnimMsg(stateHash, normalizedTime, dirtyBits, new NetworkReader(parameters));
        }

        [ClientRpc]
        void RpcOnAnimationParametersClientMessage(uint dirtyBits, byte[] parameters)
        {
            HandleAnimParamsMsg(dirtyBits, new NetworkReader(parameters));
        }

        // server sends this to one client
        [ClientRpc]
        void RpcOnAnimationTriggerClientMessage(int hash)
        {
            HandleAnimTriggerMsg(hash);
        }
        #endregion
    }
}
