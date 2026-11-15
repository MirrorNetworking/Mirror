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
        
        [Header("Bar Character")]
        [Tooltip("Character to represent filled segments of the connection quality bar")]
        public char barChar = '■';

#if !UNITY_SERVER || UNITY_EDITOR
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
            style.richText = true; // enable colored substrings
            
            GUILayout.BeginHorizontal(style);
            
            double rttMs = Math.Round(NetworkTime.rtt * 1000);
            var quality = NetworkClient.connectionQuality;
            int totalSegments = Enum.GetValues(typeof(ConnectionQuality)).Length - 1;
            int filled = Mathf.Clamp((int)quality, 0, totalSegments);

            string lit   = new string(barChar, filled);
            string unlit = new string(barChar, totalSegments - filled);
            string litColor  = ColorUtility.ToHtmlStringRGB(quality.ColorCode());
            string unlitColor = "666666";
            
            // Q: [■■■■] (lit part colored, unlit part grey)
            string bar = $"[<color=#{litColor}>{lit}</color><color=#{unlitColor}>{unlit}</color>]";
            GUILayout.Label($"RTT: {rttMs} ms");
            GUILayout.Label($"Q: {bar}");
            
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.color = Color.white;
        }
#endif
    }
}
