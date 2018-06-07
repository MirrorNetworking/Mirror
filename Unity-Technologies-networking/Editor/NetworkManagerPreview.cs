#if ENABLE_UNET
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.Networking
{
    [CustomPreview(typeof(NetworkManager))]
    class NetworkManagerPreview : ObjectPreview
    {
        NetworkManager m_Manager;
        GUIContent m_Title;

        protected GUIContent m_ShowServerMessagesLabel;
        protected GUIContent m_ShowClientMessagesLabel;

        const int k_Padding = 4;
        const int k_ColumnWidth = 120;
        const int k_RowHeight = 16;

        public override void Initialize(UnityObject[] targets)
        {
            base.Initialize(targets);
            GetNetworkInformation(target as NetworkManager);

            m_ShowServerMessagesLabel = new GUIContent("Server Message Handlers:", "Registered network message handler functions");
            m_ShowClientMessagesLabel = new GUIContent("Client Message Handlers:", "Registered network message handler functions");
        }

        public override GUIContent GetPreviewTitle()
        {
            if (m_Title == null)
            {
                m_Title = new GUIContent("NetworkManager Message Handlers");
            }
            return m_Title;
        }

        public override bool HasPreviewGUI()
        {
            return m_Manager != null;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (m_Manager == null)
                return;

            int posY = (int)(r.yMin + k_Padding);

            posY = ShowServerMessageHandlers(r, posY);
            posY = ShowClientMessageHandlers(r, posY);
        }

        static string FormatHandler(KeyValuePair<short, NetworkMessageDelegate> handler)
        {
            return string.Format("{0}:{1}()",
                handler.Value.Method.DeclaringType.Name,
                handler.Value.Method.Name);
        }

        int ShowServerMessageHandlers(Rect r, int posY)
        {
            if (NetworkServer.handlers.Count == 0)
                return posY;

            GUI.Label(new Rect(r.xMin + k_Padding, posY, 400, k_RowHeight), m_ShowServerMessagesLabel);
            posY += k_RowHeight;

            foreach (var handler in NetworkServer.handlers)
            {
                GUI.Label(new Rect(r.xMin + k_Padding * 4, posY, 400, k_RowHeight), MsgType.MsgTypeToString(handler.Key));
                GUI.Label(new Rect(r.xMin + k_Padding * 4 + k_ColumnWidth, posY, 400, k_RowHeight), FormatHandler(handler));
                posY += k_RowHeight;
            }
            return posY;
        }

        int ShowClientMessageHandlers(Rect r, int posY)
        {
            if (NetworkClient.allClients.Count == 0)
                return posY;

            NetworkClient client = NetworkClient.allClients[0];
            if (client == null)
                return posY;

            GUI.Label(new Rect(r.xMin + k_Padding, posY, 400, k_RowHeight), m_ShowClientMessagesLabel);
            posY += k_RowHeight;

            foreach (var handler in client.handlers)
            {
                GUI.Label(new Rect(r.xMin + k_Padding * 4, posY, 400, k_RowHeight), MsgType.MsgTypeToString(handler.Key));
                GUI.Label(new Rect(r.xMin + k_Padding * 4 + k_ColumnWidth, posY, 400, k_RowHeight), FormatHandler(handler));
                posY += k_RowHeight;
            }
            return posY;
        }

        void GetNetworkInformation(NetworkManager man)
        {
            m_Manager = man;
        }
    }
}
#endif //ENABLE_UNET
