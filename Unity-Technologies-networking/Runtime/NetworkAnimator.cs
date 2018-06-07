#if ENABLE_UNET
using System;
using UnityEngine;
using UnityEngine.Networking.NetworkSystem;

namespace UnityEngine.Networking
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkAnimator")]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(Animator))]
    public class NetworkAnimator : NetworkBehaviour
    {
        // configuration
        [SerializeField] Animator   m_Animator;
        [SerializeField] uint       m_ParameterSendBits;

        // static message objects to avoid runtime-allocations
        static AnimationMessage s_AnimationMessage = new AnimationMessage();
        static AnimationParametersMessage s_AnimationParametersMessage = new AnimationParametersMessage();
        static AnimationTriggerMessage s_AnimationTriggerMessage = new AnimationTriggerMessage();

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
        NetworkWriter           m_ParameterWriter;
        float                   m_SendTimer;

        // tracking - these should probably move to a Preview component.
        public string   param0;
        public string   param1;
        public string   param2;
        public string   param3;
        public string   param4;
        public string   param5;

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

                if (hasAuthority)
                    return true;

                return false;
            }
        }

        internal void ResetParameterOptions()
        {
            Debug.Log("ResetParameterOptions");
            m_ParameterSendBits = 0;
        }

        void FixedUpdate()
        {
            if (!sendMessagesAllowed)
                return;

            if (m_ParameterWriter == null)
                m_ParameterWriter = new NetworkWriter();

            CheckSendRate();

            int stateHash;
            float normalizedTime;
            if (!CheckAnimStateChanged(out stateHash, out normalizedTime))
            {
                return;
            }

            var animMsg = new AnimationMessage();
            animMsg.netId = netId;
            animMsg.stateHash = stateHash;
            animMsg.normalizedTime = normalizedTime;

            m_ParameterWriter.SeekZero();
            WriteParameters(m_ParameterWriter, false);
            animMsg.parameters = m_ParameterWriter.ToArray();

            SendMessage(MsgType.Animation, animMsg);
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
            if (sendMessagesAllowed && GetNetworkSendInterval() != 0 && m_SendTimer < Time.time)
            {
                m_SendTimer = Time.time + GetNetworkSendInterval();

                var animMsg = new AnimationParametersMessage();
                animMsg.netId = netId;

                m_ParameterWriter.SeekZero();
                WriteParameters(m_ParameterWriter, true);
                animMsg.parameters = m_ParameterWriter.ToArray();

                SendMessage(MsgType.AnimationParameters, animMsg);
            }
        }

        void SendMessage(short type, MessageBase msg)
        {
            if (isServer)
            {
                NetworkServer.SendToReady(gameObject, type, msg);
            }
            else
            {
                if (ClientScene.readyConnection != null)
                {
                    ClientScene.readyConnection.Send(type, msg);
                }
            }
        }

        void SetSendTrackingParam(string p, int i)
        {
            p = "Sent Param: " + p;
            if (i == 0) param0 = p;
            if (i == 1) param1 = p;
            if (i == 2) param2 = p;
            if (i == 3) param3 = p;
            if (i == 4) param4 = p;
            if (i == 5) param5 = p;
        }

        void SetRecvTrackingParam(string p, int i)
        {
            p = "Recv Param: " + p;
            if (i == 0) param0 = p;
            if (i == 1) param1 = p;
            if (i == 2) param2 = p;
            if (i == 3) param3 = p;
            if (i == 4) param4 = p;
            if (i == 5) param5 = p;
        }

        internal void HandleAnimMsg(AnimationMessage msg, NetworkReader reader)
        {
            if (hasAuthority)
                return;

            // usually transitions will be triggered by parameters, if not, play anims directly.
            // NOTE: this plays "animations", not transitions, so any transitions will be skipped.
            // NOTE: there is no API to play a transition(?)
            if (msg.stateHash != 0)
            {
                m_Animator.Play(msg.stateHash, 0, msg.normalizedTime);
            }

            ReadParameters(reader, false);
        }

        internal void HandleAnimParamsMsg(AnimationParametersMessage msg, NetworkReader reader)
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
            for (int i = 0; i < m_Animator.parameters.Length; i++)
            {
                if (autoSend && !GetParameterAutoSend(i))
                    continue;

                AnimatorControllerParameter par = m_Animator.parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    writer.WritePackedUInt32((uint)m_Animator.GetInteger(par.nameHash));

                    SetSendTrackingParam(par.name + ":" + m_Animator.GetInteger(par.nameHash), i);
                }

                if (par.type == AnimatorControllerParameterType.Float)
                {
                    writer.Write(m_Animator.GetFloat(par.nameHash));

                    SetSendTrackingParam(par.name + ":" + m_Animator.GetFloat(par.nameHash), i);
                }

                if (par.type == AnimatorControllerParameterType.Bool)
                {
                    writer.Write(m_Animator.GetBool(par.nameHash));

                    SetSendTrackingParam(par.name + ":" + m_Animator.GetBool(par.nameHash), i);
                }
            }
        }

        void ReadParameters(NetworkReader reader, bool autoSend)
        {
            for (int i = 0; i < m_Animator.parameters.Length; i++)
            {
                if (autoSend && !GetParameterAutoSend(i))
                    continue;

                AnimatorControllerParameter par = m_Animator.parameters[i];
                if (par.type == AnimatorControllerParameterType.Int)
                {
                    int newValue = (int)reader.ReadPackedUInt32();
                    m_Animator.SetInteger(par.nameHash, newValue);

                    SetRecvTrackingParam(par.name + ":" + newValue, i);
                }

                if (par.type == AnimatorControllerParameterType.Float)
                {
                    float newFloatValue = reader.ReadSingle();
                    m_Animator.SetFloat(par.nameHash, newFloatValue);

                    SetRecvTrackingParam(par.name + ":" + newFloatValue, i);
                }

                if (par.type == AnimatorControllerParameterType.Bool)
                {
                    bool newBoolValue = reader.ReadBoolean();
                    m_Animator.SetBool(par.nameHash, newBoolValue);

                    SetRecvTrackingParam(par.name + ":" + newBoolValue, i);
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
            var animMsg = new AnimationTriggerMessage();
            animMsg.netId = netId;
            animMsg.hash = hash;

            if (hasAuthority && localPlayerAuthority)
            {
                if (NetworkClient.allClients.Count  > 0)
                {
                    var client = ClientScene.readyConnection;
                    if (client != null)
                    {
                        client.Send(MsgType.AnimationTrigger, animMsg);
                    }
                }
                return;
            }

            if (isServer && !localPlayerAuthority)
            {
                NetworkServer.SendToReady(gameObject, MsgType.AnimationTrigger, animMsg);
            }
        }

        // ------------------ server message handlers -------------------

        static internal void OnAnimationServerMessage(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_AnimationMessage);

            if (LogFilter.logDev) { Debug.Log("OnAnimationMessage for netId=" + s_AnimationMessage.netId + " conn=" + netMsg.conn); }

            GameObject go = NetworkServer.FindLocalObject(s_AnimationMessage.netId);
            if (go == null)
            {
                return;
            }
            NetworkAnimator animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                NetworkReader reader = new NetworkReader(s_AnimationMessage.parameters);
                animSync.HandleAnimMsg(s_AnimationMessage, reader);

                NetworkServer.SendToReady(go, MsgType.Animation, s_AnimationMessage);
            }
        }

        static internal void OnAnimationParametersServerMessage(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_AnimationParametersMessage);

            if (LogFilter.logDev) { Debug.Log("OnAnimationParametersMessage for netId=" + s_AnimationParametersMessage.netId + " conn=" + netMsg.conn); }

            GameObject go = NetworkServer.FindLocalObject(s_AnimationParametersMessage.netId);
            if (go == null)
            {
                return;
            }
            NetworkAnimator animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                NetworkReader reader = new NetworkReader(s_AnimationParametersMessage.parameters);
                animSync.HandleAnimParamsMsg(s_AnimationParametersMessage, reader);
                NetworkServer.SendToReady(go, MsgType.AnimationParameters, s_AnimationParametersMessage);
            }
        }

        static internal void OnAnimationTriggerServerMessage(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_AnimationTriggerMessage);

            if (LogFilter.logDev) { Debug.Log("OnAnimationTriggerMessage for netId=" + s_AnimationTriggerMessage.netId + " conn=" + netMsg.conn); }

            GameObject go = NetworkServer.FindLocalObject(s_AnimationTriggerMessage.netId);
            if (go == null)
            {
                return;
            }
            NetworkAnimator animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                animSync.HandleAnimTriggerMsg(s_AnimationTriggerMessage.hash);

                NetworkServer.SendToReady(go, MsgType.AnimationTrigger, s_AnimationTriggerMessage);
            }
        }

        // ------------------ client message handlers -------------------

        static internal void OnAnimationClientMessage(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_AnimationMessage);
            GameObject go = ClientScene.FindLocalObject(s_AnimationMessage.netId);
            if (go == null)
            {
                return;
            }
            var animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                var reader = new NetworkReader(s_AnimationMessage.parameters);
                animSync.HandleAnimMsg(s_AnimationMessage, reader);
            }
        }

        static internal void OnAnimationParametersClientMessage(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_AnimationParametersMessage);
            GameObject go = ClientScene.FindLocalObject(s_AnimationParametersMessage.netId);
            if (go == null)
            {
                return;
            }
            var animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                var reader = new NetworkReader(s_AnimationParametersMessage.parameters);
                animSync.HandleAnimParamsMsg(s_AnimationParametersMessage, reader);
            }
        }

        static internal void OnAnimationTriggerClientMessage(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_AnimationTriggerMessage);
            GameObject go = ClientScene.FindLocalObject(s_AnimationTriggerMessage.netId);
            if (go == null)
            {
                return;
            }
            var animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                animSync.HandleAnimTriggerMsg(s_AnimationTriggerMessage.hash);
            }
        }
    }
}
#endif
