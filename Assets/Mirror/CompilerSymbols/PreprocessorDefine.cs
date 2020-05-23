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
                "MIRROR_1726_OR_NEWER",
                "MIRROR_3_0_OR_NEWER",
                "MIRROR_3_12_OR_NEWER",
                "MIRROR_4_0_OR_NEWER",
                "MIRROR_5_0_OR_NEWER",
                "MIRROR_6_0_OR_NEWER",
                "MIRROR_7_0_OR_NEWER",
                "MIRROR_8_0_OR_NEWER",
                "MIRROR_9_0_OR_NEWER",
                "MIRROR_10_0_OR_NEWER",
                "MIRROR_11_0_OR_NEWER",
                "MIRROR_12_0_OR_NEWER",
                "MIRROR_13_0_OR_NEWER"
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
