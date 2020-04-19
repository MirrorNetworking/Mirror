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
        static readonly ILogger logger = LogFactory.GetLogger(typeof(EnterPlayModeSettingsCheck));

        [InitializeOnLoadMethod]
        public static void OnInitializeOnLoad()
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
                CheckSuccessfulWeave();
            }
        }

        static void CheckSuccessfulWeave()
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
                    logger.LogError("Can't enter play mode until weaver issues are resolved.");
                    EditorApplication.isPlaying = false;
                }
            }
        }
    }
}
