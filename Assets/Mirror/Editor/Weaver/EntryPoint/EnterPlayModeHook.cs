#if !UNITY_2020_3_OR_NEWER
// make sure we weaved successfully when entering play mode.
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    public class EnterPlayModeSettingsCheck : MonoBehaviour
    {
        [InitializeOnLoadMethod]
        static void OnInitializeOnLoad()
        {
            // Hook this event to see if we have a good weave every time
            // user attempts to enter play mode or tries to do a build
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Per Unity docs, this fires "when exiting edit mode before the Editor is in play mode".
            // This doesn't fire when closing the editor.
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // Check if last weave result was successful
                if (!SessionState.GetBool("MIRROR_WEAVE_SUCCESS", false))
                {
                    // Last weave result was a failure...try to weave again
                    // Faults will show in the console that may have been cleared by "Clear on Play"
                    SessionState.SetBool("MIRROR_WEAVE_SUCCESS", true);
                    Weaver.CompilationFinishedHook.WeaveExistingAssemblies();

                    // Did that clear things up for us?
                    if (!SessionState.GetBool("MIRROR_WEAVE_SUCCESS", false))
                    {
                        // Nope, still failed, and console has the issues logged
                        Debug.LogError("Can't enter play mode until weaver issues are resolved.");
                        EditorApplication.isPlaying = false;
                    }
                }
            }
        }
    }
}
#endif
