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
            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            HashSet<string> defines = new HashSet<string>(currentDefines.Split(';'))
            {
                "MIRROR",
                "MIRROR_57_0_OR_NEWER",
                "MIRROR_58_0_OR_NEWER",
                "MIRROR_65_0_OR_NEWER",
                "MIRROR_66_0_OR_NEWER",
                "MIRROR_2022_9_OR_NEWER",
                "MIRROR_2022_10_OR_NEWER",
                "MIRROR_70_0_OR_NEWER",
                "MIRROR_71_0_OR_NEWER",
                "MIRROR_73_OR_NEWER",
                "MIRROR_78_OR_NEWER"
                // Remove oldest when adding next month's symbol.
                // Keep a rolling 12 months of symbols.
            };

            // only touch PlayerSettings if we actually modified it,
            // otherwise it shows up as changed in git each time.
            string newDefines = string.Join(";", defines);
            if (newDefines != currentDefines)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, newDefines);
            }
        }
    }
}
