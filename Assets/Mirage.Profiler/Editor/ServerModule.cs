using Unity.Profiling;
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.UIElements;

namespace Mirage.NetworkProfiler.ModuleGUI
{
    [System.Serializable]
    [ProfilerModuleMetadata(ModuleNames.SERVER)]
    public class ServerModule : ProfilerModule
    {
        private static readonly ProfilerCounterDescriptor[] counters = new ProfilerCounterDescriptor[]
        {
            new ProfilerCounterDescriptor(Names.PLAYER_COUNT, Counters.Category),
            new ProfilerCounterDescriptor(Names.CHARACTER_COUNT, Counters.Category),
            new ProfilerCounterDescriptor(Names.OBJECT_COUNT, Counters.Category),
        };

        public ServerModule() : base(counters) { }

        public override ProfilerModuleViewController CreateDetailsViewController()
        {
            return new ServerViewController(ProfilerWindow);
        }
    }

    public abstract class BaseViewController : ProfilerModuleViewController
    {
        public BaseViewController(ProfilerWindow profilerWindow) : base(profilerWindow) { }

        protected static Label AddLabelWithPadding(VisualElement parent)
        {
            var label = new Label() { style = { paddingTop = 8, paddingLeft = 8 } };
            parent.Add(label);
            return label;
        }


        protected void SetText(Label label, string name)
        {
            var frame = (int)ProfilerWindow.selectedFrameIndex;
            var category = ProfilerCategory.Network.Name;
            var value = ProfilerDriver.GetFormattedCounterValue(frame, category, name);

            label.text = $"{name}: {value}";
        }
    }

    public sealed class ServerViewController : BaseViewController
    {
        private Label PlayerLabel;
        private Label CharacterLabel;
        private Label ObjectLabel;

        public ServerViewController(ProfilerWindow profilerWindow) : base(profilerWindow) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();

            PlayerLabel = AddLabelWithPadding(root);
            CharacterLabel = AddLabelWithPadding(root);
            ObjectLabel = AddLabelWithPadding(root);

            PlayerLabel.tooltip = Names.PLAYER_COUNT_TOOLTIP;
            CharacterLabel.tooltip = Names.CHARACTER_COUNT_TOOLTIP;
            ObjectLabel.tooltip = Names.OBJECT_COUNT_TOOLTIP;

            // Populate the label with the current data for the selected frame. 
            ReloadData();

            // Be notified when the selected frame index in the Profiler Window changes, so we can update the label.
            ProfilerWindow.SelectedFrameIndexChanged += OnSelectedFrameIndexChanged;

            return root;
        }

        private void OnSelectedFrameIndexChanged(long selectedFrameIndex)
        {
            // Update the label with the current data for the newly selected frame.
            ReloadData();
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            // Unsubscribe from the Profiler window event that we previously subscribed to.
            ProfilerWindow.SelectedFrameIndexChanged -= OnSelectedFrameIndexChanged;

            base.Dispose(disposing);
        }

        private void ReloadData()
        {
            SetText(PlayerLabel, Names.PLAYER_COUNT);
            SetText(CharacterLabel, Names.CHARACTER_COUNT);
            SetText(ObjectLabel, Names.OBJECT_COUNT);
        }
    }
}
