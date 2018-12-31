using System;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(NetworkTransform), true)]
    [CanEditMultipleObjects]
    public class NetworkTransformEditor : Editor
    {
        private static GUIContent[] axisOptions = {new GUIContent("None"), new GUIContent("X"), new GUIContent("Y (Top-Down 2D)"), new GUIContent("Z (Side-on 2D)"), new GUIContent("XY (FPS)"), new GUIContent("XZ"), new GUIContent("YZ"), new GUIContent("XYZ (full 3D)")};

        bool m_Initialized;
        NetworkTransform m_SyncTransform;

        SerializedProperty m_TransformSyncMode;
        SerializedProperty m_MovementTheshold;
        SerializedProperty m_VelocityThreshold;
        SerializedProperty m_SnapThreshold;

        SerializedProperty m_InterpolateRotation;
        SerializedProperty m_InterpolateMovement;
        SerializedProperty m_RotationSyncCompression;
        SerializedProperty m_SyncSpin;

        protected GUIContent m_MovementThesholdLabel;
        protected GUIContent m_VelocityThresholdLabel;
        protected GUIContent m_SnapThresholdLabel;

        protected GUIContent m_InterpolateRotationLabel;
        protected GUIContent m_InterpolateMovementLabel;
        protected GUIContent m_RotationSyncCompressionLabel;
        protected GUIContent m_RotationAxisLabel;
        protected GUIContent m_SyncSpinLabel;

        SerializedProperty m_SyncIntervalProperty;

        public void Init()
        {
            if (m_Initialized)
                return;

            m_Initialized = true;
            m_SyncTransform = target as NetworkTransform;

            if (m_SyncTransform.transformSyncMode == NetworkTransform.TransformSyncMode.SyncNone)
            {
                if (m_SyncTransform.GetComponent<Rigidbody>() != null)
                {
                    m_SyncTransform.transformSyncMode = NetworkTransform.TransformSyncMode.SyncRigidbody3D;
                    m_SyncTransform.syncRotationAxis = NetworkTransform.AxisSyncMode.AxisXYZ;
                    EditorUtility.SetDirty(m_SyncTransform);
                }
                else if (m_SyncTransform.GetComponent<Rigidbody2D>() != null)
                {
                    m_SyncTransform.transformSyncMode = NetworkTransform.TransformSyncMode.SyncRigidbody2D;
                    m_SyncTransform.syncRotationAxis = NetworkTransform.AxisSyncMode.AxisZ;
                    EditorUtility.SetDirty(m_SyncTransform);
                }
                else if (m_SyncTransform.GetComponent<CharacterController>() != null)
                {
                    m_SyncTransform.transformSyncMode = NetworkTransform.TransformSyncMode.SyncCharacterController;
                    m_SyncTransform.syncRotationAxis = NetworkTransform.AxisSyncMode.AxisXYZ;
                    EditorUtility.SetDirty(m_SyncTransform);
                }
                else
                {
                    m_SyncTransform.transformSyncMode = NetworkTransform.TransformSyncMode.SyncTransform;
                    m_SyncTransform.syncRotationAxis = NetworkTransform.AxisSyncMode.AxisXYZ;
                    EditorUtility.SetDirty(m_SyncTransform);
                }
            }
            m_TransformSyncMode = serializedObject.FindProperty("m_TransformSyncMode");
            m_MovementTheshold = serializedObject.FindProperty("m_MovementTheshold");
            m_VelocityThreshold = serializedObject.FindProperty("m_VelocityThreshold");
            m_SnapThreshold = serializedObject.FindProperty("m_SnapThreshold");

            m_InterpolateRotation = serializedObject.FindProperty("m_InterpolateRotation");
            m_InterpolateMovement = serializedObject.FindProperty("m_InterpolateMovement");
            m_RotationSyncCompression = serializedObject.FindProperty("m_RotationSyncCompression");
            m_SyncSpin = serializedObject.FindProperty("m_SyncSpin");

            m_SyncIntervalProperty = serializedObject.FindProperty("syncInterval");
            EditorGUI.indentLevel += 1;
            m_MovementThesholdLabel = new GUIContent("Movement Threshold", "The distance that this object can move without sending a movement synchronization update.");
            m_VelocityThresholdLabel = new GUIContent("Velocity Threshold", "The minimum velocity difference that will be synchronized over the network.");
            m_SnapThresholdLabel = new GUIContent("Snap Threshold", "If a movement update puts this object further from its current position that this value, it will snap to the updated position instead of moving smoothly.");

            m_InterpolateRotationLabel = new GUIContent("Interpolate Rotation Factor", "The larger this number is, the faster the object will interpolate to the target facing direction.");
            m_InterpolateMovementLabel = new GUIContent("Interpolate Movement Factor", "The larger this number is, the faster the object will interpolate to the target position.");
            m_RotationSyncCompressionLabel = new GUIContent("Compress Rotation", "How much to compress rotation sync updates.\n\nChoose None for no compression.\n\nChoose Low for a low amount of compression that preserves accuracy.\n\nChoose High for a high amount of compression that sacrifices accuracy.");
            m_RotationAxisLabel = new GUIContent("Rotation Axis", "Which axis to use for rotation.");
            m_SyncSpinLabel = new GUIContent("Sync Angular Velocity", "Enable to sync angular velocity.");
            EditorGUI.indentLevel -= 1;
        }

        protected void ShowControls()
        {
            if (m_TransformSyncMode == null)
            {
                m_Initialized = false;
            }
            Init();

            serializedObject.Update();

            // [0,2] should be enough. anything >2s is too laggy anyway.
            m_SyncIntervalProperty.floatValue = EditorGUILayout.Slider(
                new GUIContent("Network Sync Interval",
                    "Time in seconds until next change is synchronized to the client. '0' means send immediately if changed. '0.5' means only send changes every 500ms.\n(This is for state synchronization like SyncVars, SyncLists, OnSerialize. Not for Cmds, Rpcs, etc.)"),
                m_SyncIntervalProperty.floatValue, 0, 2);

            EditorGUILayout.PropertyField(m_TransformSyncMode);
            if (m_TransformSyncMode.enumValueIndex == (int)NetworkTransform.TransformSyncMode.SyncRigidbody3D)
            {
                Rigidbody r3D = m_SyncTransform.GetComponent<Rigidbody>();
                if (r3D == null)
                {
                    Debug.LogError("Object has no Rigidbody component.");
                    m_TransformSyncMode.enumValueIndex = (int)NetworkTransform.TransformSyncMode.SyncTransform;
                    EditorUtility.SetDirty(m_SyncTransform);
                }
            }
            if (m_TransformSyncMode.enumValueIndex == (int)NetworkTransform.TransformSyncMode.SyncRigidbody2D)
            {
                Rigidbody2D r2D = m_SyncTransform.GetComponent<Rigidbody2D>();
                if (r2D == null)
                {
                    Debug.LogError("Object has no Rigidbody2D component.");
                    m_TransformSyncMode.enumValueIndex = (int)NetworkTransform.TransformSyncMode.SyncTransform;
                    EditorUtility.SetDirty(m_SyncTransform);
                }
            }
            if (m_TransformSyncMode.enumValueIndex == (int)NetworkTransform.TransformSyncMode.SyncCharacterController)
            {
                var cc = m_SyncTransform.GetComponent<CharacterController>();
                if (cc == null)
                {
                    Debug.LogError("Object has no CharacterController component.");
                    m_TransformSyncMode.enumValueIndex = (int)NetworkTransform.TransformSyncMode.SyncTransform;
                    EditorUtility.SetDirty(m_SyncTransform);
                }
            }

            EditorGUILayout.LabelField("Movement:");
            EditorGUI.indentLevel += 1;
            EditorGUILayout.PropertyField(m_MovementTheshold, m_MovementThesholdLabel);

            if (m_VelocityThreshold.floatValue < 0)
            {
                m_VelocityThreshold.floatValue = 0;
                EditorUtility.SetDirty(m_SyncTransform);
            }

            if ((m_TransformSyncMode.enumValueIndex == (int)NetworkTransform.TransformSyncMode.SyncRigidbody3D) || (m_TransformSyncMode.enumValueIndex == (int)NetworkTransform.TransformSyncMode.SyncRigidbody2D))
            {
                EditorGUILayout.PropertyField(m_VelocityThreshold, m_VelocityThresholdLabel);
            }

            if (m_MovementTheshold.floatValue < 0)
            {
                m_MovementTheshold.floatValue = 0;
                EditorUtility.SetDirty(m_SyncTransform);
            }
            EditorGUILayout.PropertyField(m_SnapThreshold, m_SnapThresholdLabel);
            EditorGUILayout.PropertyField(m_InterpolateMovement, m_InterpolateMovementLabel);
            EditorGUI.indentLevel -= 1;

            EditorGUILayout.LabelField("Rotation:");
            EditorGUI.indentLevel += 1;

            int newRotation = EditorGUILayout.Popup(
                    m_RotationAxisLabel,
                    (int)m_SyncTransform.syncRotationAxis,
                    axisOptions);
            if ((NetworkTransform.AxisSyncMode)newRotation != m_SyncTransform.syncRotationAxis)
            {
                m_SyncTransform.syncRotationAxis = (NetworkTransform.AxisSyncMode)newRotation;
                EditorUtility.SetDirty(m_SyncTransform);
            }

            EditorGUILayout.PropertyField(m_InterpolateRotation, m_InterpolateRotationLabel);
            EditorGUILayout.PropertyField(m_RotationSyncCompression, m_RotationSyncCompressionLabel);
            EditorGUILayout.PropertyField(m_SyncSpin, m_SyncSpinLabel);

            EditorGUI.indentLevel -= 1;

            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            ShowControls();
        }
    }
}
