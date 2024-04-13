using UnityEditor;

namespace Mirror
{
    [CustomEditor(typeof(LagCompensator))]
    public class LagCompensatorInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Preview Component - Feedback appreciated on GitHub or Discord!", MessageType.Warning);
            DrawDefaultInspector();
        }
    }
}
