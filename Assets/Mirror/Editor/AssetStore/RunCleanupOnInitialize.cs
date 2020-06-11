using UnityEditor;
using UnityEngine;

namespace Mirror.EditorScripts
{
    public static class RunCleanupOnInitialize
    {
        [InitializeOnLoadMethod]
        public static void OnProjectLoadedInEditor()
        {
            Debug.Log("Cleaning up old Scripts");

            CleanupOldScripts.TryDeleteOldScripts(true);
        }
    }
}
