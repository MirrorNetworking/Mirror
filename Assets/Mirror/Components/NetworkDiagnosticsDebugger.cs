using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/Network Diagnostics Debugger")]
    public class NetworkDiagnosticsDebugger : MonoBehaviour
    {
        public bool logInMessages = true;
        public bool logOutMessages = true;
        void OnInMessage(NetworkDiagnostics.MessageInfo msgInfo)
        {
            if (logInMessages)
                Debug.Log(msgInfo);
        }
        void OnOutMessage(NetworkDiagnostics.MessageInfo msgInfo)
        {
            if (logOutMessages)
                Debug.Log(msgInfo);
        }
        void OnEnable()
        {
            NetworkDiagnostics.InMessageEvent += OnInMessage;
            NetworkDiagnostics.OutMessageEvent += OnOutMessage;
        }
        void OnDisable()
        {
            NetworkDiagnostics.InMessageEvent -= OnInMessage;
            NetworkDiagnostics.OutMessageEvent -= OnOutMessage;
        }
    }
}
