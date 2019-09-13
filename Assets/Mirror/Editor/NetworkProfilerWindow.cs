using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    public class NetworkProfilerWindow : EditorWindow
    {
        public int activeConfigIndex;
        private GUIStyle headerStyle;
        private Vector2 leftScrollPosition;
        private Vector2 rightScrollPosition;

        private const int ChannelColumnWidth = 100;
        private const int MessageColumnWidth = 100;

        private List<NetworkProfileTick> ticks = new List<NetworkProfileTick>();
        private NetworkProfileTick selectedTick;
        

        public GUIStyle columnHeaderStyle { get; private set; }

        public void OnEnable()
        {
            NetworkProfiler.TickRecorded += this.OnTickRecorded;
        }        

        public void OnDisable()
        {
            NetworkProfiler.TickRecorded -= this.OnTickRecorded;
        }

        public void OnGUI()
        {
            if (this.headerStyle == null)
            {
                this.headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = 14 };
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(250));

            NetworkProfiler.IsRecording = GUILayout.Toggle(NetworkProfiler.IsRecording, "Record", "Button");
            EditorGUILayout.Space();

            this.leftScrollPosition = EditorGUILayout.BeginScrollView(this.leftScrollPosition);

            for (int x = this.ticks.Count - 1; x > -1; x--)
            {
                NetworkProfileTick t = this.ticks[x];
                if (GUILayout.Button($"{t.Time} - {t.TotalMessages} Messages ")) { this.Show(t); };
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Tick Inspector Window
            EditorGUILayout.BeginVertical();
            this.rightScrollPosition = EditorGUILayout.BeginScrollView(this.rightScrollPosition);
            if (this.selectedTick != null)
            {
                EditorGUILayout.LabelField($"Time : {this.selectedTick.Time}", this.headerStyle);

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Channels", this.headerStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Channel", GUILayout.Width(ChannelColumnWidth));
                GUILayout.Label("Bytes In", GUILayout.Width(ChannelColumnWidth));
                GUILayout.Label("Bytes Out", GUILayout.Width(ChannelColumnWidth));
                GUILayout.EndHorizontal();
                foreach (NetworkProfileChannel c in this.selectedTick.Channels)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(c.Id.ToString(), GUILayout.Width(ChannelColumnWidth));
                    GUILayout.Label(c.BytesIncoming.ToString(), GUILayout.Width(ChannelColumnWidth));
                    GUILayout.Label(c.BytesOutgoing.ToString(), GUILayout.Width(ChannelColumnWidth));
                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Messages", this.headerStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Direction", GUILayout.Width(75));
                GUILayout.Label("Count", GUILayout.Width(75));
                GUILayout.Label("Type", GUILayout.Width(MessageColumnWidth * 4));
                GUILayout.Label("Name", GUILayout.Width(MessageColumnWidth * 4));
                GUILayout.EndHorizontal();
                foreach (NetworkProfileMessage m in this.selectedTick.Messages)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(m.Direction.ToString(), GUILayout.Width(75));
                    GUILayout.Label(m.Count.ToString(), GUILayout.Width(75));
                    GUILayout.Label(m.Type.ToString(), GUILayout.Width(MessageColumnWidth * 4));
                    GUILayout.Label(m.Name.ToString(), GUILayout.Width(MessageColumnWidth * 4));
                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();
                
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            this.Repaint();
        }

        [MenuItem("Window/Analysis/Mirror Network Profiler", false, 0)]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<NetworkProfilerWindow>("Mirror Network Profiler");
        }

        public void Show(NetworkProfileTick tick)
        {
            this.selectedTick = tick;
        }

        private void OnTickRecorded(NetworkProfileTick obj)
        {
            this.ticks.Add(obj);

            if (this.ticks.Count > 300)
            {
                this.ticks.RemoveAt(0);
            }
        }

        public static void DrawUILine(Color color, int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            r.height = thickness;
            r.y += padding / 2;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, color);
        }
    }
}
