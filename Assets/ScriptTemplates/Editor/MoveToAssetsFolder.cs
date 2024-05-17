using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class MoveToAssetsFolder
{
    const string FirstTimeKey = "MOVE_SCRIPT_TEMPLATES_HAS_RUN";
    const string targetFolder = "ScriptTemplates";
    const string targetPath = "Assets/ScriptTemplates";

    static MoveToAssetsFolder()
    {
        if (!SessionState.GetBool(FirstTimeKey, false))
        {
            FindAndMoveScriptTemplatesFolder();
            SessionState.SetBool(FirstTimeKey, true);
        }
    }

    static void FindAndMoveScriptTemplatesFolder()
    {
        string[] guids = AssetDatabase.FindAssets(targetFolder, null);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Check if it's a folder and not some random asset
            if (AssetDatabase.IsValidFolder(path))
            {
                // Ensure exact match of the name and that it's not in the Assets folder already
                string folderName = System.IO.Path.GetFileName(path);
                if (folderName == targetFolder && !path.StartsWith(targetPath))
                {
                    AssetDatabase.MoveAsset(path, targetPath);
                    Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"Moved {targetFolder} to Assets folder.");
                }
            }
        }

        AssetDatabase.Refresh();
    }
}
