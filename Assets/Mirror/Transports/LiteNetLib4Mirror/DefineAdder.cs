#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace Mirror
{
	static class DefineAdder
	{
		/// <summary>
		/// Add define symbols as soon as Unity gets done compiling.
		/// </summary>
		[InitializeOnLoadMethod]
		static void AddDefineSymbols()
		{
			HashSet<string> defines = new HashSet<string>(PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup).Split(';'))
			{
				"LITENETLIB4MIRROR"
			};
			PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.Join(";", defines));
		}
	}
}
#endif
