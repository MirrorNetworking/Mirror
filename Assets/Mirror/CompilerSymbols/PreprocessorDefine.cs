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
            HashSet<string> defines = new HashSet<string>(PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup).Split(';'))
            {
                "MIRROR",
                "MIRROR_1726_OR_NEWER",
                "MIRROR_3_0_OR_NEWER",
                "MIRROR_3_12_OR_NEWER",
                "MIRROR_4_0_OR_NEWER",
                "MIRROR_5_0_OR_NEWER",
                "MIRROR_6_0_OR_NEWER",
                "MIRROR_7_0_OR_NEWER",
                "MIRROR_8_0_OR_NEWER"
            };
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.Join(";", defines));
        }
    }
}
