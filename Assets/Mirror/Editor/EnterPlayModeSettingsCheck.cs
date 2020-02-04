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
        [InitializeOnLoadMethod]
        static void OnInitializeOnLoad()
        {
            // check immediately on load
            //CheckSuccessfulWeave();

#if UNITY_2019_3_OR_NEWER
      CheckPlayModeOptions();
#endif

            // check each time we press play. OnLoad is only called once and
            // wouldn't detect editor-time setting changes
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // only check when entering play mode. no need to show it again
            // when exiting.
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                CheckSuccessfulWeave();
#if UNITY_2019_3_OR_NEWER
               CheckPlayModeOptions();
#endif
            }
        }

        static void CheckSuccessfulWeave()
        {
            bool weaved = SessionState.GetBool("MIRROR_WEAVED", true);
            if (!weaved)
            {
                // try to weave again...faults will show in the console that may have been cleared by "Clear on Play"
                Weaver.CompilationFinishedHook.WeaveExistingAssemblies();
            }

            weaved = SessionState.GetBool("MIRROR_WEAVED", true);
            if (!weaved)
            {
                // still failed, and console has the issues logged
                Debug.LogError("Can't enter play mode until weaver issues are resolved.");
                EditorApplication.isPlaying = false;
            }
        }

#if UNITY_2019_3_OR_NEWER
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
