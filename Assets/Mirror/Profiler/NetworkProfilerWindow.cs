using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Mirror.Profiler.Chart;
using Mirror.Profiler.Table;
using System.Linq;

namespace Mirror.Profiler
{
    public class NetworkProfilerWindow : EditorWindow
    {
        public enum ChartSeries {
            MessageCount,
            TotalBytes,
            Bandwidth
        }

        public const int MaxFrames = 1000;
        public const int MaxTicks = 300;

        private const string SaveEditorPrefKey = "Mirror.NetworkProfilerWindow.Record";
        private const string CaptureFileExtension = "netdata";
        private const float EstimatedOverheadPerMessage = 70f;

        public int activeConfigIndex;
        private GUIStyle headerStyle;
        private Vector2 leftScrollPosition;
        private readonly NetworkProfiler networkProfiler = new NetworkProfiler(MaxFrames);
        internal NetworkProfileTick selectedTick;
        TreeModel<MyTreeElement> treeModel;

        private readonly ChartView chart;
        
        public GUIStyle columnHeaderStyle { get; private set; }


        public NetworkProfilerWindow()
        {
            var inCount = GetCountSeriesByFrame("Inbound (count)", NetworkDirection.Incoming);
            var outCount = GetCountSeriesByFrame("Outbound (count)", NetworkDirection.Outgoing);

            chart = new ChartView(inCount,outCount)
            {
                MaxFrames = MaxTicks,
            };

            chart.OnSelectFrame += Chart_OnSelectFrame;
        }

        private void Chart_OnSelectFrame(int frame)
        {
            if (ShowAllFrames)
            {
                NetworkProfileTick t = networkProfiler.GetTick(frame);
                Show(t);
            }
            else if (frame >= 0 && frame < networkProfiler.Ticks.Count)
            {
                NetworkProfileTick t = networkProfiler.Ticks[frame];
                Show(t);
            }
            else
            {
                Show(new NetworkProfileTick());
            }
        }

#region Render window

        [NonSerialized] bool m_Initialized;
        [SerializeField] TreeViewState m_TreeViewState; // Serialized in the window layout file so it survives assembly reloading
        [SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
        MultiColumnTreeView m_TreeView;

        void InitIfNeeded()
        {
            if (!m_Initialized)
            {
                // Check if it already exists (deserialized from window layout file or scriptable object)
                if (m_TreeViewState == null)
                    m_TreeViewState = new TreeViewState();

                bool firstInit = m_MultiColumnHeaderState == null;
                MultiColumnHeaderState headerState = MultiColumnTreeView.CreateDefaultMultiColumnHeaderState();
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
                m_MultiColumnHeaderState = headerState;

                MultiColumnHeader multiColumnHeader = new MultiColumnHeader(headerState);
                if (firstInit)
                    multiColumnHeader.ResizeToFit();

                treeModel = new TreeModel<MyTreeElement>(GetData(selectedTick));

                m_TreeView = new MultiColumnTreeView(m_TreeViewState, multiColumnHeader, treeModel);

                m_Initialized = true;
            }
        }

        IList<MyTreeElement> GetData(NetworkProfileTick tick)
        {
            // generate some test data
            return MyTreeElementGenerator.GenerateTable(tick);
        }

        void DoTreeView(Rect rect)
        {
            InitIfNeeded();
            m_TreeView.OnGUI(rect);
        }

        private void OnEnable()
        {
            this.networkProfiler.IsRecording = EditorPrefs.GetBool(SaveEditorPrefKey, false);
            ConfigureProfiler();
        }

        public void OnGUI()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = 14 };
            }

            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            this.DrawCommandBar();

            Rect rect = GUILayoutUtility.GetRect(10, 1000, 200, 200);

            this.chart.OnGUI(rect);

            Rect treeRect = GUILayoutUtility.GetRect(new GUIContent(), new GUIStyle(), GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            this.DoTreeView(treeRect);

            EditorGUILayout.EndVertical();

            this.Repaint();
        }

        private void DrawCommandBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));

            bool newValue = GUILayout.Toggle(networkProfiler.IsRecording, "Record", EditorStyles.toolbarButton);

            if (newValue != networkProfiler.IsRecording)
            {
                EditorPrefs.SetBool(SaveEditorPrefKey, newValue);
            }

            networkProfiler.IsRecording = newValue;

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
            {
                this.networkProfiler.Clear();
            }

            if (GUILayout.Button("Save", EditorStyles.toolbarButton))
            {
                string path = EditorUtility.SaveFilePanel("Save Network Profile", null, "Capture", CaptureFileExtension);
                if (!string.IsNullOrEmpty(path))
                {
                    this.networkProfiler.Save(path);
                }
            }

            if (GUILayout.Button("Load", EditorStyles.toolbarButton))
            {
                string path = EditorUtility.OpenFilePanel("Open Network Profile", null, CaptureFileExtension);
                if (!string.IsNullOrEmpty(path))
                {
                    this.networkProfiler.Load(path);

                    if (this.networkProfiler.Ticks.Count > 0)
                    {
                        Show(networkProfiler.Ticks[0]);
                        this.ConfigureProfiler();
                    }
                }
            }
            GUILayout.FlexibleSpace();

            var leftIcon = EditorGUIUtility.TrIconContent("Animation.PrevKey", "Previous Messages");
            if (GUILayout.Button(leftIcon, EditorStyles.toolbarButton))
            {
                if (ShowAllFrames)
                {
                    NetworkProfileTick tick = networkProfiler.GetPrevMessageTick(chart.SelectedFrame);

                    chart.SelectedFrame = tick.frameCount;
                }
                else
                {
                    chart.SelectedFrame -= 1;
                }
            }

            var rightIcon = EditorGUIUtility.TrIconContent("Animation.NextKey", "Next Messages");
            if (GUILayout.Button(rightIcon, EditorStyles.toolbarButton))
            {
                if (ShowAllFrames)
                {
                    NetworkProfileTick tick = networkProfiler.GetNextMessageTick(chart.SelectedFrame);

                    chart.SelectedFrame = tick.frameCount;
                }
                else
                {
                    chart.SelectedFrame += 1;
                }
            }

            var optionsIcon = EditorGUIUtility.TrIconContent("_Popup", "Options");
            if (EditorGUILayout.DropdownButton(optionsIcon , FocusType.Passive, EditorStyles.toolbarButton))
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("Message count"), Series == ChartSeries.MessageCount, OnSeriesSelected, ChartSeries.MessageCount);
                menu.AddItem(new GUIContent("Total bytes"), Series == ChartSeries.TotalBytes, OnSeriesSelected, ChartSeries.TotalBytes);
                menu.AddItem(new GUIContent("Estimated Bandwidth"), Series == ChartSeries.Bandwidth, OnSeriesSelected, ChartSeries.Bandwidth);
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Show all frames"), ShowAllFrames, () =>
                {
                    ShowAllFrames = !ShowAllFrames;
                });

                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
        }


        #region Chart options

        private void OnSeriesSelected(object series)
        {
            EditorPrefs.SetString("Mirror.Profiler.Series", series.ToString());
            ConfigureProfiler();
        }

        public ChartSeries Series =>
            (ChartSeries)Enum.Parse(typeof(ChartSeries), EditorPrefs.GetString("Mirror.Profiler.Series", ChartSeries.MessageCount.ToString()));

        public bool ShowAllFrames
        {
            get => EditorPrefs.GetBool("Mirror.Profiler.AllFrames", false);
            set
            {
                EditorPrefs.SetBool("Mirror.Profiler.AllFrames", value);
                ConfigureProfiler();
            }
        }

        private void ConfigureProfiler()
        {
            ISeries inSeries;
            ISeries outSeries;

            if (ShowAllFrames)
            {
                networkProfiler.MaxFrames = MaxFrames;
                networkProfiler.MaxTicks = int.MaxValue;
                chart.MaxFrames = MaxFrames;

                (inSeries, outSeries) = AllFrameSeries();
            }
            else
            {
                networkProfiler.MaxFrames = int.MaxValue;
                networkProfiler.MaxTicks = MaxTicks;
                chart.MaxFrames = MaxTicks;

                (inSeries, outSeries) = AllTicksSeries();
            }

            chart.Series[0] = inSeries;
            chart.Series[1] = outSeries;
        }

        private (ISeries, ISeries) AllFrameSeries()
        {
            switch (Series)
            {
                case ChartSeries.TotalBytes:
                    return (
                        GetByteSeriesByFrame("Inbound (bytes)", NetworkDirection.Incoming),
                        GetByteSeriesByFrame("Outbound (bytes)", NetworkDirection.Outgoing));

                case ChartSeries.MessageCount:
                    return (
                         GetCountSeriesByFrame("Inbound (bytes)", NetworkDirection.Incoming),
                         GetCountSeriesByFrame("Outbound (bytes)", NetworkDirection.Outgoing));

                case ChartSeries.Bandwidth:
                    return (
                        GetBandwidthSeriesByFrame("Inbound (bytes/s)", NetworkDirection.Incoming),
                        GetBandwidthSeriesByFrame("Outbound (bytes/s)", NetworkDirection.Outgoing));

                default:
                    return (
                        GetByteSeriesByFrame("Inbound (bytes)", NetworkDirection.Incoming),
                        GetByteSeriesByFrame("Outbound (bytes)", NetworkDirection.Outgoing));
            }
        }

        private (ISeries, ISeries) AllTicksSeries()
        {
            switch (Series)
            {
                case ChartSeries.TotalBytes:
                    return (
                        GetByteSeriesByIndex("Inbound (bytes)", NetworkDirection.Incoming),
                        GetByteSeriesByIndex("Outbound (bytes)", NetworkDirection.Outgoing));
                case ChartSeries.MessageCount:
                    return (
                        GetCountSeriesByIndex("Inbound (bytes)", NetworkDirection.Incoming),
                        GetCountSeriesByIndex("Outbound (bytes)", NetworkDirection.Outgoing));
                case ChartSeries.Bandwidth:
                    return (
                        GetBandwidthSeriesByIndex("Inbound (bytes/s)", NetworkDirection.Incoming),
                        GetBandwidthSeriesByIndex("Outbound (bytes/s)", NetworkDirection.Outgoing));
                default:
                    throw new Exception("Invalid chart type");
            }
        }


        private ISeries GetCountSeriesByFrame(string legend, NetworkDirection direction)
        {
            var data = networkProfiler.Ticks.Select(tick => (tick.frameCount, (float)tick.Count(direction)));

            // append the current frame
            var lastTick = Enumerable.Range(0, 1).Select(_ =>
            {
                 var tick = networkProfiler.CurrentTick();
                 return (tick.frameCount, (float)tick.Count(direction));
            });

            return new Series(legend, data.Concat(lastTick));
        }

        private ISeries GetByteSeriesByFrame(string legend, NetworkDirection direction)
        {
            var data = networkProfiler.Ticks.Select(tick => (tick.frameCount, (float)tick.Bytes(direction)));

            // append the current frame
            var lastTick = Enumerable.Range(0, 1).Select(_ =>
            {
                var tick = networkProfiler.CurrentTick();
                return (tick.frameCount, (float)tick.Bytes(direction));
            });

            return new Series(legend, data.Concat(lastTick));
        }

        private ISeries GetCountSeriesByIndex(string legend, NetworkDirection direction)
        {
            var data = networkProfiler.Ticks.Select((tick, i) => (i, (float)tick.Count(direction)));

            return new Series(legend, data);
        }

        private ISeries GetByteSeriesByIndex(string legend, NetworkDirection direction)
        {
            var data = networkProfiler.Ticks.Select((tick, i) => (i, (float)tick.Bytes(direction)));

            return new Series(legend, data);
        }

        private ISeries GetBandwidthSeriesByIndex(string legend, NetworkDirection direction)
        {
            var tickData = networkProfiler.Ticks;
            var tick1 = new[]
            {
                new NetworkProfileTick
                {
                    frameCount = 0,
                    time =0
                }
            };
            var allTicks = tick1.Concat(tickData);

            var data = allTicks
                .Zip(tickData, (prevTick, newTick) => EstimateBandwidth(newTick, prevTick, direction))
                .Select((value, i) => (i - 1, value));

            return new Series(legend, data);
        }

        private float EstimateBandwidth(NetworkProfileTick tick, NetworkProfileTick prevTick, NetworkDirection direction)
        {
            float estimatedBytes = tick.Count(direction) * EstimatedOverheadPerMessage + tick.Bytes(direction);

            if (prevTick.frameCount == 0 || tick.time <= prevTick.time)
                return estimatedBytes;
            
            return estimatedBytes / (tick.time - prevTick.time);
        }

        private ISeries GetBandwidthSeriesByFrame(string legend, NetworkDirection direction)
        {
            var data = networkProfiler.Ticks.Select(tick => (tick.frameCount, tick.Bytes(direction) + tick.Count(direction) * EstimatedOverheadPerMessage));

            // append the current frame
            var lastTick = Enumerable.Range(0, 1).Select(_ =>
            {
                var tick = networkProfiler.CurrentTick();
                return (tick.frameCount, tick.Bytes(direction) + tick.Count(direction) * EstimatedOverheadPerMessage);
            });

            return new Series(legend, data.Concat(lastTick));

        }

        #endregion


        [MenuItem("Window/Analysis/Mirror Network Profiler", false, 0)]
        public static void ShowWindow()
        {
            GetWindow<NetworkProfilerWindow>("Mirror Network Profiler");
        }

        public void Show(NetworkProfileTick tick)
        {
            selectedTick = tick;
            treeModel.SetData(GetData(selectedTick));
        }

#endregion
    }
}
