// Unity 2019.3 has an experimental 'disable domain reload on play'
// feature. keeping any global state between sessions will break
// Mirror and most of our user's projects. don't allow it for now.
// https://blogs.unity3d.com/2019/11/05/enter-play-mode-faster-in-unity-2019-3/
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    public class EnterPlayModeSettingsCheck : MonoBehaviour
    {
#if UNITY_2019_3_OR_NEWER
        [InitializeOnLoadMethod]
        static void OnInitializeOnLoad()
        {
            // check immediately on load
            CheckPlayModeOptions();

            // check each time we press play. OnLoad is only called once and
            // wouldn't detect editor-time setting changes
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // only check when entering play mode. no need to show it again
            // when exiting.
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                CheckPlayModeOptions();
            }
        }

        static void CheckPlayModeOptions()
        {
            // enabling the checkbox is enough. it controls all the other
            // settings.
            if (EditorSettings.enterPlayModeOptionsEnabled)
            {
                Debug.LogError("Enter Play Mode Options are not supported by Mirror. Please disable 'ProjectSettings->Editor->Enter Play Mode Settings (Experimental)'.");
                EditorApplication.isPlaying = false;
            }
        }
#endif
    }
}
