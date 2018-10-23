using UnityEditor;
using UnityEngine;
using System.Collections;

namespace Mirror
{
    [CustomEditor(typeof(NetworkTransformChild), true)]
    [CanEditMultipleObjects]
    public class NetworkTransformChildEditor : Editor
    {
        private static GUIContent[] axisOptions = {new GUIContent("None"), new GUIContent("X"), new GUIContent("Y (Top-Down 2D)"), new GUIContent("Z (Side-on 2D)"), new GUIContent("XY (FPS)"), new GUIContent("XZ"), new GUIContent("YZ"), new GUIContent("XYZ (full 3D)")};

        bool m_Initialized;
        NetworkTransformChild sync;

        SerializedProperty m_Target;
        SerializedProperty m_MovementThreshold;

        SerializedProperty m_InterpolateRotation;
        SerializedProperty m_InterpolateMovement;
        SerializedProperty m_RotationSyncCompression;

        protected GUIContent m_TargetLabel;
        protected GUIContent m_MovementThresholdLabel;

        protected GUIContent m_InterpolateRotationLabel;
        protected GUIContent m_InterpolateMovementLabel;
        protected GUIContent m_RotationSyncCompressionLabel;
        protected GUIContent m_RotationAxisLabel;

        SerializedProperty m_NetworkSendIntervalProperty;
        GUIContent m_NetworkSendIntervalLabel;

        public void Init()
        {
            if (m_Initialized)
                return;

            m_Initialized = true;
            sync = target as NetworkTransformChild;

            m_Target = serializedObject.FindProperty("m_Target");
            if (sync.GetComponent<NetworkTransform>() == null)
            {
                Debug.LogError("NetworkTransformChild must be on the root object with the NetworkTransform, not on the child node");
                m_Target.objectReferenceValue = null;
            }

            m_MovementThreshold = serializedObject.FindProperty("m_MovementThreshold");

            m_InterpolateRotation = serializedObject.FindProperty("m_InterpolateRotation");
            m_InterpolateMovement = serializedObject.FindProperty("m_InterpolateMovement");
            m_RotationSyncCompression = serializedObject.FindProperty("m_RotationSyncCompression");

            m_NetworkSendIntervalProperty = serializedObject.FindProperty("m_SendInterval");

            m_TargetLabel = new GUIContent("Target", "The child transform to be synchronized.");
            m_NetworkSendIntervalLabel = new GUIContent("Network Send Rate", "Number of network updates per second.");
            EditorGUI.indentLevel += 1;
            m_MovementThresholdLabel = new GUIContent("Movement Threshold", "The distance that this object can move without sending a movement synchronization update.");

            m_InterpolateRotationLabel = new GUIContent("Interpolate Rotation Factor", "The larger this number is, the faster the object will interpolate to the target facing direction.");
            m_InterpolateMovementLabel = new GUIContent("Interpolate Movement Factor", "The larger this number is, the faster the object will interpolate to the target position.");
            m_RotationSyncCompressionLabel = new GUIContent("Compress Rotation", "How much to compress rotation sync updates.\n\nChoose None for no compression.\n\nChoose Low for a low amount of compression that preserves accuracy.\n\nChoose High for a high amount of compression that sacrifices accuracy.");
            m_RotationAxisLabel = new GUIContent("Rotation Axis", "Which axis to use for rotation.");

            EditorGUI.indentLevel -= 1;
        }

        protected void ShowControls()
        {
            if (m_Target == null)
            {
                m_Initialized = false;
            }
            Init();

            serializedObject.Update();

            int sendRate = 0;
            if (m_NetworkSendIntervalProperty.floatValue != 0)
            {
                sendRate = (int)(1 / m_NetworkSendIntervalProperty.floatValue);
            }
            int newSendRate = EditorGUILayout.IntSlider(m_NetworkSendIntervalLabel, sendRate, 0, 30);
            if (newSendRate != sendRate)
            {
                if (newSendRate == 0)
                {
                    m_NetworkSendIntervalProperty.floatValue = 0;
                }
                else
                {
                    m_NetworkSendIntervalProperty.floatValue = 1.0f / newSendRate;
                }
            }
            if (EditorGUILayout.PropertyField(m_Target, m_TargetLabel))
            {
                if (sync.GetComponent<NetworkTransform>() == null)
                {
                    Debug.LogError("NetworkTransformChild must be on the root object with the NetworkTransform, not on the child node");
                    m_Target.objectReferenceValue = null;
                }
            }

            EditorGUILayout.PropertyField(m_MovementThreshold, m_MovementThresholdLabel);
            if (m_MovementThreshold.floatValue < 0)
            {
                m_MovementThreshold.floatValue = 0;
                EditorUtility.SetDirty(sync);
            }
            EditorGUILayout.PropertyField(m_InterpolateMovement, m_InterpolateMovementLabel);

            EditorGUILayout.PropertyField(m_InterpolateRotation, m_InterpolateRotationLabel);

            int newRotation = EditorGUILayout.Popup(
                    m_RotationAxisLabel,
                    (int)sync.syncRotationAxis,
                    axisOptions);
            if ((NetworkTransform.AxisSyncMode)newRotation != sync.syncRotationAxis)
            {
                sync.syncRotationAxis = (NetworkTransform.AxisSyncMode)newRotation;
                EditorUtility.SetDirty(sync);
            }

            EditorGUILayout.PropertyField(m_RotationSyncCompression, m_RotationSyncCompressionLabel);

            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            ShowControls();
        }
    }
}
