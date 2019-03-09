using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(NetworkAnimator), true)]
    [CanEditMultipleObjects]
    public class NetworkAnimatorEditor : Editor
    {
        NetworkAnimator m_AnimSync;
        [NonSerialized] bool m_Initialized;

        SerializedProperty  m_AnimatorProperty;
        GUIContent m_AnimatorLabel = new GUIContent("Animator", "The Animator component to synchronize.");

        void Init()
        {
            if (m_Initialized)
                return;

            m_Initialized = true;
            m_AnimSync = target as NetworkAnimator;

            m_AnimatorProperty = serializedObject.FindProperty("m_Animator");
        }

        public override void OnInspectorGUI()
        {
            Init();
            serializedObject.Update();
            DrawControls();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawControls()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_AnimatorProperty, m_AnimatorLabel);
            if (EditorGUI.EndChangeCheck())
            {
                m_AnimSync.ResetParameterOptions();
            }

            if (m_AnimSync.animator == null)
                return;

            AnimatorController controller = m_AnimSync.animator.runtimeAnimatorController as AnimatorController;
            if (controller != null)
            {
                bool showWarning = false;
                EditorGUI.indentLevel += 1;
                int i = 0;

                foreach (AnimatorControllerParameter p in controller.parameters)
                {
                    if (i >= 32)
                    {
                        showWarning = true;
                        break;
                    }

                    bool oldSend = m_AnimSync.GetParameterAutoSend(i);
                    bool send = EditorGUILayout.Toggle("Sync " + p.name, oldSend);
                    if (send != oldSend)
                    {
                        m_AnimSync.SetParameterAutoSend(i, send);
                        EditorUtility.SetDirty(target);
                    }
                    i += 1;
                }

                if (showWarning)
                {
                    EditorGUILayout.HelpBox("NetworkAnimator can only select between the first 32 parameters in a mecanim controller", MessageType.Warning);
                }

                EditorGUI.indentLevel -= 1;
            }
        }
    }
}
