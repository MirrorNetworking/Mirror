using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Component that will display the clients ping in milliseconds
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/Network Ping Display")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-ping-display")]
    public class NetworkPingDisplay : MonoBehaviour
    {
        public Color color = Color.white;
        public int padding = 2;
        public int width = 150;
        public int height = 25;

        void OnGUI()
        {
            // only while client is active
            if (!NetworkClient.active) return;

            // show stats in bottom right corner, right aligned
            GUI.color = color;
            Rect rect = new Rect(Screen.width - width - padding, Screen.height - height - padding, width, height);
            GUILayout.BeginArea(rect);
            GUIStyle style = GUI.skin.GetStyle("Label");
            style.alignment = TextAnchor.MiddleRight;
            GUILayout.BeginHorizontal(style);
                GUILayout.Label($"RTT: {Math.Round(NetworkTime.rtt * 1000)}ms");
                GUI.color = NetworkClient.connectionQuality.ColorCode();
                GUILayout.Label($"Q: {new string('-', (int)NetworkClient.connectionQuality)}");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.color = Color.white;
        }
    }
}
