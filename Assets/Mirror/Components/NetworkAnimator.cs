using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkAnimator")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkAnimator")]
    public class NetworkAnimator : NetworkBehaviour
    {
        // configuration
        [FormerlySerializedAs("m_Animator")] public Animator animator;
        // Note: not an object[] array because otherwise initialization is real annoying
        int[] lastIntParameters;
        float[] lastFloatParameters;
        bool[] lastBoolParameters;
        AnimatorControllerParameter[] parameters;

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

        void Awake()
        {
            // store the animator parameters in a variable - the "Animator.parameters" getter allocates
            // a new parameter array every time it is accessed so we should avoid doing it in a loop
            parameters = animator.parameters;
            lastIntParameters = new int[parameters.Length];
            lastFloatParameters = new float[parameters.Length];
            lastBoolParameters = new bool[parameters.Length];
        }

        void FixedUpdate()
        {
            if (!sendMessagesAllowed)
                return;

            CheckSendRate();

            if (!CheckAnimStateChanged(out int stateHash, out float normalizedTime))
            {
                return;
            }

            NetworkWriter writer = new NetworkWriter();
            WriteParameters(writer);

            SendAnimationMessage(stateHash, normalizedTime, writer.ToArray());
        }

        bool CheckAnimStateChanged(out int stateHash, out float normalizedTime)
        {
            stateHash = 0;
            normalizedTime = 0;

            if (animator.IsInTransition(0))
            {
                AnimatorTransitionInfo tt = animator.GetAnimatorTransitionInfo(0);
                if (tt.fullPathHash != m_TransitionHash)
                {
                    // first time in this transition
                    m_TransitionHash = tt.fullPathHash;
                    m_AnimationHash = 0;
                    return true;
                }
                return false;
            }

            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
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

        void CheckSendRate()
        {
            if (sendMessagesAllowed && syncInterval != 0 && m_SendTimer < Time.time)
            {
                m_SendTimer = Time.time + syncInterval;

                NetworkWriter writer = new NetworkWriter();
                if (WriteParameters(writer))
                {
                    SendAnimationParametersMessage(writer.ToArray());
                }
            }
        }

        void SendAnimationMessage(int stateHash, float normalizedTime, byte[] parameters)
        {
            if (isServer)
            {
                RpcOnAnimationClientMessage(stateHash, normalizedTime, parameters);
            }
            else if (ClientScene.readyConnection != null)
            {
                CmdOnAnimationServerMessage(stateHash, normalizedTime, parameters);
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

        void HandleAnimMsg(int stateHash, float normalizedTime, NetworkReader reader)
        {
            if (hasAuthority)
                return;

            // usually transitions will be triggered by parameters, if not, play anims directly.
            // NOTE: this plays "animations", not transitions, so any transitions will be skipped.
            // NOTE: there is no API to play a transition(?)
            if (stateHash != 0)
            {
                animator.Play(stateHash, 0, normalizedTime);
            }

            ReadParameters(reader);
        }

        void HandleAnimParamsMsg(NetworkReader reader)
        {
            if (hasAuthority)
                return;

            ReadParameters(reader);
        }

        void HandleAnimTriggerMsg(int hash)
        {
            animator.SetTrigger(hash);
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
                    {
                        lastIntParameters[i] = newIntValue;
                    }
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = animator.GetFloat(par.nameHash);
                    changed = Mathf.Abs(newFloatValue - lastFloatParameters[i]) > 0.001f;
                    if (changed)
                    {
                        lastFloatParameters[i] = newFloatValue;
                    }
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = animator.GetBool(par.nameHash);
                    changed = newBoolValue != lastBoolParameters[i];
                    if (changed)
                    {
                        lastBoolParameters[i] = newBoolValue;
                    }
                }
                if (changed)
                {
                    dirtyBits |= 1ul << i;
                }
            }
            return dirtyBits;
        }

        bool WriteParameters(NetworkWriter writer)
        {
            ulong dirtyBits = NextDirtyBits();
            writer.WritePackedUInt64(dirtyBits);
            for (int i = 0; i < parameters.Length; i++)
            {
                if ((dirtyBits & (1ul << i)) == 0)
                    continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newIntValue = animator.GetInteger(par.nameHash);
                    writer.WritePackedInt32(newIntValue);
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = animator.GetFloat(par.nameHash);
                    writer.Write(newFloatValue);
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = animator.GetBool(par.nameHash);
                    writer.Write(newBoolValue);
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
                    animator.SetInteger(par.nameHash, newIntValue);
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = reader.ReadSingle();
                    animator.SetFloat(par.nameHash, newFloatValue);
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = reader.ReadBoolean();
                    animator.SetBool(par.nameHash, newBoolValue);
                }
            }
        }

        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            if (forceAll)
            {
                if (animator.IsInTransition(0))
                {
                    AnimatorStateInfo st = animator.GetNextAnimatorStateInfo(0);
                    writer.Write(st.fullPathHash);
                    writer.Write(st.normalizedTime);
                }
                else
                {
                    AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
                    writer.Write(st.fullPathHash);
                    writer.Write(st.normalizedTime);
                }
                WriteParameters(writer);
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
                ReadParameters(reader);
                animator.Play(stateHash, 0, normalizedTime);
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
                if (ClientScene.readyConnection != null)
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
        void CmdOnAnimationServerMessage(int stateHash, float normalizedTime, byte[] parameters)
        {
            if (LogFilter.Debug) Debug.Log("OnAnimationMessage for netId=" + netId);

            // handle and broadcast
            HandleAnimMsg(stateHash, normalizedTime, new NetworkReader(parameters));
            RpcOnAnimationClientMessage(stateHash, normalizedTime, parameters);
        }

        [Command]
        void CmdOnAnimationParametersServerMessage(byte[] parameters)
        {
            // handle and broadcast
            HandleAnimParamsMsg(new NetworkReader(parameters));
            RpcOnAnimationParametersClientMessage(parameters);
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
        void RpcOnAnimationClientMessage(int stateHash, float normalizedTime, byte[] parameters)
        {
            HandleAnimMsg(stateHash, normalizedTime, new NetworkReader(parameters));
        }

        [ClientRpc]
        void RpcOnAnimationParametersClientMessage(byte[] parameters)
        {
            HandleAnimParamsMsg(new NetworkReader(parameters));
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
