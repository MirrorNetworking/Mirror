#if ENABLE_UNET
using UnityEditor;
using UnityEditor.Connect;
using UnityEditor.Web;

namespace UnityEditor
{
    [InitializeOnLoad]
    internal class NetworkServiceInitialize
    {
        static NetworkServiceInitialize()
        {
            UnityEditor.Analytics.CoreStats.OnRequireInBuildHandler += () =>
                {
                    string[] guids = new string[]
                    {
                        "870353891bb340e2b2a9c8707e7419ba", // UnityEngine.Networking.dll
                        "dc443db3e92b4983b9738c1131f555cb" // Standalone/UnityEngine.Networking.dll
                    };

                    foreach (var g in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(g);
                        if (!string.IsNullOrEmpty(path))
                        {
                            string[] references = BuildPipeline.GetReferencingPlayerAssembliesForDLL(path);
                            if (references.Length > 0)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                };
        }
    }
}
#endif
