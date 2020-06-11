using UnityEditor;

namespace Mirror.EditorScripts
{
    public static class RunCleanupOnInitialize
    {
        [InitializeOnLoadMethod]
        public static void OnProjectLoadedInEditor()
        {
            CleanupOldScripts.TryDeleteOldScripts(true);
        }
    }
}
