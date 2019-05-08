using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(NetworkAnimator), true)]
    [CanEditMultipleObjects]
    public class NetworkAnimatorEditor : Editor
    {
		SerializedProperty animatorProperty;
		SerializedProperty interpolateFloatParametersProperty;
		SerializedProperty interpolationFactorProperty;

		bool initialized;

		void Init()
        {
			if (initialized)
            {
                return;
            }

            animatorProperty = serializedObject.FindProperty("animator");
            interpolateFloatParametersProperty = serializedObject.FindProperty("interpolateFloatParameters");
            interpolationFactorProperty = serializedObject.FindProperty("interpolationFactor");
        }

		public override void OnInspectorGUI()
        {
			Init();

			NetworkBehaviour networkBehaviour = target as NetworkBehaviour;

			serializedObject.Update();

			EditorGUILayout.PropertyField(animatorProperty);
			EditorGUILayout.PropertyField(interpolateFloatParametersProperty, new GUIContent("Enable Interpolation", "Enable interpolation (float parameters only)"));
			if (interpolateFloatParametersProperty.boolValue)
			{
				EditorGUILayout.PropertyField(interpolationFactorProperty);
			}
			
			// Grabbed from NetworkBehaviourInspector.cs
			if (networkBehaviour != null)
            {
                // [0,2] should be enough. anything >2s is too laggy anyway.
                serializedObject.FindProperty("syncInterval").floatValue = EditorGUILayout.Slider(
                    new GUIContent("Network Sync Interval",
                                    "Time in seconds until next change is synchronized to the client. '0' means send immediately if changed. '0.5' means only send changes every 500ms.\n(This is for state synchronization like SyncVars, SyncLists, OnSerialize. Not for Cmds, Rpcs, etc.)"),
                    networkBehaviour.syncInterval, 0, 2);
                serializedObject.ApplyModifiedProperties();
            }

			serializedObject.ApplyModifiedProperties();
		}
	}
}