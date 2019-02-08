using System;
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkAnimator")]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(Animator))]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkAnimator")]
    public class NetworkAnimator : NetworkBehaviour
    {
        // configuration
        [SerializeField] Animator   m_Animator;
        [SerializeField] uint       m_ParameterSendBits;

        // properties
        public Animator animator
        {
            get { return m_Animator; }
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
                m_ParameterSendBits |=  (uint)(1 << index);
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

        int                     m_AnimationHash;
        int                     m_TransitionHash;
        float                   m_SendTimer;

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

        public void ResetParameterOptions()
        {
            Debug.Log("ResetParameterOptions");
            m_ParameterSendBits = 0;
        }

        void FixedUpdate()
        {
            if (!sendMessagesAllowed)
                return;

            CheckSendRate();

            int stateHash;
            float normalizedTime;
            if (!CheckAnimStateChanged(out stateHash, out normalizedTime))
            {
                return;
            }

            NetworkWriter writer = new NetworkWriter();
            WriteParameters(writer, false);

            SendAnimationMessage(stateHash, normalizedTime, writer.ToArray());
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

        void CheckSendRate()
        {
            if (sendMessagesAllowed && syncInterval != 0 && m_SendTimer < Time.time)
            {
                m_SendTimer = Time.time + syncInterval;

                NetworkWriter writer = new NetworkWriter();
                WriteParameters(writer, true);

                SendAnimationParametersMessage(writer.ToArray());
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

        internal void HandleAnimMsg(int stateHash, float normalizedTime, NetworkReader reader)
        {
            if (hasAuthority)
                return;

            // usually transitions will be triggered by parameters, if not, play anims directly.
            // NOTE: this plays "animations", not transitions, so any transitions will be skipped.
            // NOTE: there is no API to play a transition(?)
            if (stateHash != 0)
            {
                m_Animator.Play(stateHash, 0, normalizedTime);
            }

            ReadParameters(reader, false);
        }

        internal void HandleAnimParamsMsg(NetworkReader reader)
        {
            if (hasAuthority)
                return;

            ReadParameters(reader, true);
        }

        internal void HandleAnimTriggerMsg(int hash)
        {
            m_Animator.SetTrigger(hash);
        }

        void WriteParameters(NetworkWriter writer, bool autoSend)
        {
            // store the animator parameters in a variable - the "Animator.parameters" getter allocates
            // a new parameter array every time it is accessed so we should avoid doing it in a loop
            AnimatorControllerParameter[] parameters = m_Animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (autoSend && !GetParameterAutoSend(i))
                    continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    writer.WritePackedUInt32((uint)m_Animator.GetInteger(par.nameHash));
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

        void ReadParameters(NetworkReader reader, bool autoSend)
        {
            // store the animator parameters in a variable - the "Animator.parameters" getter allocates
            // a new parameter array every time it is accessed so we should avoid doing it in a loop
            AnimatorControllerParameter[] parameters = m_Animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (autoSend && !GetParameterAutoSend(i))
                    continue;

                AnimatorControllerParameter par = parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newValue = (int)reader.ReadPackedUInt32();
                    m_Animator.SetInteger(par.nameHash, newValue);
                }
                else if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = reader.ReadSingle();
                    m_Animator.SetFloat(par.nameHash, newFloatValue);
                }
                else if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = reader.ReadBoolean();
                    m_Animator.SetBool(par.nameHash, newBoolValue);
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
                WriteParameters(writer, false);
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
                ReadParameters(reader, false);
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
                if (NetworkClient.allClients.Count > 0 && ClientScene.readyConnection != null)
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

        // ------------------ server message handlers -------------------

        [Command]
        void CmdOnAnimationServerMessage(int stateHash, float normalizedTime, byte[] parameters)
        {
            if (LogFilter.Debug) { Debug.Log("OnAnimationMessage for netId=" + netId); }

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

        // ------------------ client message handlers -------------------
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
    }
}
