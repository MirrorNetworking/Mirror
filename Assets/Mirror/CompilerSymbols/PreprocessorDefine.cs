using System.Collections.Generic;
using UnityEditor;

namespace Mirror
{
    static class PreprocessorDefine
    {
        /// <summary>
        /// Add define symbols as soon as Unity gets done compiling.
        /// </summary>
        [InitializeOnLoadMethod]
        public static void AddDefineSymbols()
        {
#if UNITY_2021_2_OR_NEWER
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
#else
            // Deprecated in Unity 2023.1
            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
#endif
            // Remove oldest when adding next month's symbol.
            // Keep a rolling 12 months of symbols.
            HashSet<string> defines = new HashSet<string>(currentDefines.Split(';'))
            {
                "MIRROR",
                "MIRROR_79_OR_NEWER",
                "MIRROR_81_OR_NEWER",
                "MIRROR_82_OR_NEWER",
                "MIRROR_83_OR_NEWER",
                "MIRROR_84_OR_NEWER",
                "MIRROR_85_OR_NEWER",
                "MIRROR_86_OR_NEWER",
                "MIRROR_89_OR_NEWER"
            };

            // only touch PlayerSettings if we actually modified it,
            // otherwise it shows up as changed in git each time.
            string newDefines = string.Join(";", defines);
            if (newDefines != currentDefines)
            {
#if UNITY_2021_2_OR_NEWER
                PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), newDefines);
#else
                // Deprecated in Unity 2023.1
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, newDefines);
#endif
            }
        }
    }
}
