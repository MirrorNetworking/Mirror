using UnityEditor;

namespace Mirror.Logging
{
    [CustomEditor(typeof(LogSettings))]
    public class LogSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            LogFactoryGUI.DrawLogFactoryDictionary(target as LogSettings);
        }
    }
}
