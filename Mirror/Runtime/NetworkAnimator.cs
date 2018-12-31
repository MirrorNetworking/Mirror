using System;
using UnityEngine;

namespace Mirror
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

            var animMsg = new AnimationMessage();
            animMsg.netId = netId;
            animMsg.stateHash = stateHash;
            animMsg.normalizedTime = normalizedTime;

            NetworkWriter writer = new NetworkWriter();
            WriteParameters(writer, false);
            animMsg.parameters = writer.ToArray();

            SendMessage((short)MsgType.Animation, animMsg);
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
            if (sendMessagesAllowed && sendInterval != 0 && m_SendTimer < Time.time)
            {
                m_SendTimer = Time.time + sendInterval;

                var animMsg = new AnimationParametersMessage();
                animMsg.netId = netId;

                NetworkWriter writer = new NetworkWriter();
                WriteParameters(writer, true);
                animMsg.parameters = writer.ToArray();

                SendMessage((short)MsgType.AnimationParameters, animMsg);
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
                        client.Send((short)MsgType.AnimationTrigger, animMsg);
                    }
                }
                return;
            }

            if (isServer && !localPlayerAuthority)
            {
                NetworkServer.SendToReady(gameObject, (short)MsgType.AnimationTrigger, animMsg);
            }
        }

        // ------------------ server message handlers -------------------

        internal static void OnAnimationServerMessage(NetworkMessage netMsg)
        {
            AnimationMessage msg = netMsg.ReadMessage<AnimationMessage>();
            if (LogFilter.Debug) { Debug.Log("OnAnimationMessage for netId=" + msg.netId + " conn=" + netMsg.conn); }

            GameObject go = NetworkServer.FindLocalObject(msg.netId);
            if (go == null)
            {
                return;
            }
            NetworkAnimator animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                NetworkReader reader = new NetworkReader(msg.parameters);
                animSync.HandleAnimMsg(msg, reader);

                NetworkServer.SendToReady(go, (short)MsgType.Animation, msg);
            }
        }

        internal static void OnAnimationParametersServerMessage(NetworkMessage netMsg)
        {
            AnimationParametersMessage msg = netMsg.ReadMessage<AnimationParametersMessage>();

            if (LogFilter.Debug) { Debug.Log("OnAnimationParametersMessage for netId=" + msg.netId + " conn=" + netMsg.conn); }

            GameObject go = NetworkServer.FindLocalObject(msg.netId);
            if (go == null)
            {
                return;
            }
            NetworkAnimator animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                NetworkReader reader = new NetworkReader(msg.parameters);
                animSync.HandleAnimParamsMsg(msg, reader);
                NetworkServer.SendToReady(go, (short)MsgType.AnimationParameters, msg);
            }
        }

        internal static void OnAnimationTriggerServerMessage(NetworkMessage netMsg)
        {
            AnimationTriggerMessage msg = netMsg.ReadMessage<AnimationTriggerMessage>();
            if (LogFilter.Debug) { Debug.Log("OnAnimationTriggerMessage for netId=" + msg.netId + " conn=" + netMsg.conn); }

            GameObject go = NetworkServer.FindLocalObject(msg.netId);
            if (go == null)
            {
                return;
            }
            NetworkAnimator animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                animSync.HandleAnimTriggerMsg(msg.hash);

                NetworkServer.SendToReady(go, (short)MsgType.AnimationTrigger, msg);
            }
        }

        // ------------------ client message handlers -------------------

        internal static void OnAnimationClientMessage(NetworkMessage netMsg)
        {
            AnimationMessage msg = netMsg.ReadMessage<AnimationMessage>();

            GameObject go = ClientScene.FindLocalObject(msg.netId);
            if (go == null)
            {
                return;
            }
            var animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                var reader = new NetworkReader(msg.parameters);
                animSync.HandleAnimMsg(msg, reader);
            }
        }

        internal static void OnAnimationParametersClientMessage(NetworkMessage netMsg)
        {
            AnimationParametersMessage msg = netMsg.ReadMessage<AnimationParametersMessage>();

            GameObject go = ClientScene.FindLocalObject(msg.netId);
            if (go == null)
            {
                return;
            }
            var animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                var reader = new NetworkReader(msg.parameters);
                animSync.HandleAnimParamsMsg(msg, reader);
            }
        }

        internal static void OnAnimationTriggerClientMessage(NetworkMessage netMsg)
        {
            AnimationTriggerMessage msg = netMsg.ReadMessage<AnimationTriggerMessage>();

            GameObject go = ClientScene.FindLocalObject(msg.netId);
            if (go == null)
            {
                return;
            }
            var animSync = go.GetComponent<NetworkAnimator>();
            if (animSync != null)
            {
                animSync.HandleAnimTriggerMsg(msg.hash);
            }
        }
    }
}
