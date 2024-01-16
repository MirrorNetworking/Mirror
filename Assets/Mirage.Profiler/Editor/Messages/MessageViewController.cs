using System;
using System.Collections.Generic;
using Mirage.NetworkProfiler.ModuleGUI.UITable;
using Unity.Profiling;
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mirage.NetworkProfiler.ModuleGUI.Messages
{
    internal sealed class MessageViewController : ProfilerModuleViewController
    {
        private readonly CounterNames _names;
        private readonly Columns _columns = new Columns();
        private Label _countLabel;
        private Label _bytesLabel;
        private Label _perSecondLabel;
        private VisualElement _toggleBox;
        private Toggle _debugToggle;
        private Toggle _groupMsgToggle;
        private MessageView _messageView;
        private readonly SavedData _savedData;

        public MessageViewController(ProfilerWindow profilerWindow, CounterNames names, SavedData savedData)
            : base(profilerWindow)
        {
            _names = names;
            _savedData = savedData;
        }

        protected override VisualElement CreateView()
        {
            // unity doesn't catch errors here so we have to wrap in try/catch
            try
            {
                return CreateViewInternal();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            // Unsubscribe from the Profiler window event that we previously subscribed to.
            ProfilerWindow.SelectedFrameIndexChanged -= FrameIndexChanged;

            base.Dispose(disposing);
        }

        private VisualElement CreateViewInternal()
        {
            var root = new VisualElement();
            root.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
            root.style.height = Length.Percent(100);
            root.style.overflow = Overflow.Hidden;

            var summary = new VisualElement();
            _countLabel = AddLabelWithPadding(summary);
            _bytesLabel = AddLabelWithPadding(summary);
            _perSecondLabel = AddLabelWithPadding(summary);
            _perSecondLabel.tooltip = Names.PER_SECOND_TOOLTIP;
            root.Add(summary);
            summary.style.height = Length.Percent(100);
            summary.style.width = 180;
            summary.style.minWidth = 180;
            summary.style.maxWidth = 180;
            summary.style.borderRightColor = Color.white * .4f;//dark grey
            summary.style.borderRightWidth = 3;

            _toggleBox = new VisualElement();
            _toggleBox.style.position = Position.Absolute;
            _toggleBox.style.bottom = 5;
            _toggleBox.style.left = 5;
            _toggleBox.style.unityTextAlign = TextAnchor.LowerLeft;
            summary.Add(_toggleBox);

            _groupMsgToggle = new Toggle
            {
                text = "Group Messages",
                tooltip = "Groups Message by type",
                value = _savedData.GroupMessages,
            };
            _groupMsgToggle.RegisterValueChangedCallback(GroupToggled);
            _toggleBox.Add(_groupMsgToggle);

            // todo allow selection of multiple frames
            //var frameSlider = new MinMaxSlider();
            //frameSlider.highLimit = 300;
            //frameSlider.lowLimit = 1;
            //frameSlider.value = Vector2.one;
            //frameSlider.RegisterValueChangedCallback(_ => Debug.Log(frameSlider.value));
            //_toggleBox.Add(frameSlider);

            _debugToggle = new Toggle
            {
                text = "Show Fake Messages",
                tooltip = "Adds fakes message to table to debug layout of table",
                value = false
            };
            _debugToggle.RegisterValueChangedCallback(_ => ReloadData());
            _toggleBox.Add(_debugToggle);
#if MIRAGE_PROFILER_DEBUG
            _debugToggle.style.display = DisplayStyle.Flex;
#else
            _debugToggle.style.display = DisplayStyle.None;
#endif


            var sorter = new TableSorter(this);
            _messageView = new MessageView(_columns, sorter, root);
            _messageView.OnGroupExpanded += OnGroupExpanded;

            // Populate the label with the current data for the selected frame. 
            ReloadData();

            // Be notified when the selected frame index in the Profiler Window changes, so we can update the label.
            ProfilerWindow.SelectedFrameIndexChanged += FrameIndexChanged;

            return root;
        }

        private void GroupToggled(ChangeEvent<bool> evt)
        {
            _savedData.GroupMessages = evt.newValue;
            ReloadData();
        }

        private void OnGroupExpanded(Group group, bool expanded)
        {
            if (expanded)
                _savedData.Expanded.Add(group.Name);
            else
                _savedData.Expanded.Remove(group.Name);
        }

        private void FrameIndexChanged(long selectedFrameIndex) => ReloadData();

        private static Label AddLabelWithPadding(VisualElement parent)
        {
            var label = new Label() { style = { paddingTop = 8, paddingLeft = 8 } };
            parent.Add(label);
            return label;
        }

        internal void Sort(SortHeader header)
        {
            _savedData.SetSortHeader(header);
            SortFromSaveData();
        }

        private void SortFromSaveData()
        {
            var (sortHeader, sortMode) = _savedData.GetSortHeader(_columns);
            _messageView.Sort(sortHeader, sortMode);
        }


        private void ReloadData()
        {
            SetSummary(_countLabel, _names.Count);
            SetSummary(_bytesLabel, _names.Bytes);
            SetSummary(_perSecondLabel, _names.PerSecond);

            ReloadMessages();
        }

        private void SetSummary(Label label, string counterName)
        {
            var frame = (int)ProfilerWindow.selectedFrameIndex;
            var category = ProfilerCategory.Network.Name;
            var value = ProfilerDriver.GetFormattedCounterValue(frame, category, counterName);

            // replace prefix
            var display = counterName.Replace("Received", "").Replace("Sent", "").Trim();
            label.text = $"{display}: {value}";
        }

        private void ReloadMessages()
        {
            const int EditorID = -1;

            _messageView.Clear();

            var frameIndex = (int)ProfilerWindow.selectedFrameIndex;
            // Debug.Log($"ReloadMessages [selected {(int)ProfilerWindow.selectedFrameIndex}]");


            if (ProfilerDriver.connectedProfiler != EditorID)
            {
                AddErrorLabel("Can't read message from player");
                return;
            }

            if (!TryGetMessages(frameIndex, out var messages))
            {
                AddErrorLabel("Can not load messages! (Message list only visible in play mode)\nIMPORTANT: make sure NetworkProfilerBehaviour is setup in starting scene");
                return;
            }

            if (messages.Count == 0)
            {
                AddInfoLabel("No Messages");
                return;
            }

            var frame = new Frame[1] {
                new Frame{ Messages = messages },
            };
            _messageView.Draw(frame, _groupMsgToggle.value);
            _messageView.ExpandMany(_savedData.Expanded);
            SortFromSaveData();
        }

        private bool TryGetMessages(int frameIndex, out List<MessageInfo> messages)
        {
#if MIRAGE_PROFILER_DEBUG
            if (_debugToggle.value)
            {
                messages = GenerateDebugMessages();
                return true;
            }
#endif

            if (frameIndex == -1)
            {
                messages = null;
                return false;
            }

            messages = _savedData.Frames.GetFrame(frameIndex).Messages;
            return true;
        }

#if MIRAGE_PROFILER_DEBUG
        private static List<MessageInfo> GenerateDebugMessages()
        {
            var messages = new List<MessageInfo>();
            var order = 0;
            for (var i = 0; i < 5; i++)
            {
                messages.Add(NewInfo(order++, new RpcMessage { netId = (uint)i }, 20 + i, 5));
                messages.Add(NewInfo(order++, new SpawnMessage { netId = (uint)i }, 80 + i, 1));
                messages.Add(NewInfo(order++, new SpawnMessage { netId = (uint)i }, 60 + i, 4));
                messages.Add(NewInfo(order++, new NetworkPingMessage { }, 4, 1));

                static MessageInfo NewInfo(int order, object msg, int bytes, int count)
                {
#if MIRAGE_DIAGNOSTIC_INSTANCE
                    return new MessageInfo(new NetworkDiagnostics.MessageInfo(null, msg, bytes, count), provider, order);
#else
                    return new MessageInfo(new NetworkDiagnostics.MessageInfo(msg, bytes, count), new NetworkInfoProvider(null), order);
#endif
                }
            }

            return messages;
        }
#endif


        private void AddErrorLabel(string message)
        {
            var parent = _messageView.AddEmptyRow();
            var ele = AddLabelWithPadding(parent);
            ele.style.color = Color.red;
            ele.text = message;
        }

        private void AddInfoLabel(string message)
        {
            var parent = _messageView.AddEmptyRow();
            var ele = AddLabelWithPadding(parent);
            ele.text = message;
        }
    }
}
