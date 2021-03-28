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
                "MIRROR_17_0_OR_NEWER",
                "MIRROR_18_0_OR_NEWER",
                "MIRROR_24_0_OR_NEWER",
                "MIRROR_26_0_OR_NEWER",
                "MIRROR_27_0_OR_NEWER",
                "MIRROR_28_0_OR_NEWER",
                "MIRROR_29_0_OR_NEWER",
                "MIRROR_30_0_OR_NEWER",
                "MIRROR_30_5_2_OR_NEWER",
                "MIRROR_32_1_2_OR_NEWER",
                "MIRROR_32_1_4_OR_NEWER",
                "MIRROR_35_0_OR_NEWER",
                "MIRROR_35_1_OR_NEWER"
            };

            // only touch PlayerSettings if we actually modified it.
            // otherwise it shows up as changed in git each time.
            string newDefines = string.Join(";", defines);
            if (newDefines != currentDefines)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, newDefines);
            }
        }
    }
}
