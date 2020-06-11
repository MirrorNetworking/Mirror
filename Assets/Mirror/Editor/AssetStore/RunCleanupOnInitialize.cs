using UnityEditor;
using UnityEngine;

namespace Mirror.EditorScripts
{
    public static class RunCleanupOnInitialize
    {
        [InitializeOnLoadMethod]
        public static void OnProjectLoadedInEditor()
        {
            bool showPrompt = !Application.isBatchMode;
            CleanupOldScripts.TryDeleteOldScripts(showPrompt);
        }
    }
}
