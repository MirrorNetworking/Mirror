using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mirror.EditorScripts
{
    public static class CleanupOldScripts
    {
        const string RuntimeFolderGuid = "9f4328ccc5f724e45afe2215d275b5d5";
        /// <summary>
        /// Only try to delete old scripts if marker file exist
        /// <para>This file will be deleted after old scripts</para>
        /// </summary>
        const string MarkerFile = "DeleteMovedAssets.txt";
        static readonly string[] oldScripts = new string[] {
            "ClientScene.cs",
            "CustomAttributes.cs",
            "DotNetCompatibility.cs",
            "ExponentialMovingAverage.cs",
            "FloatBytePacker.cs",
            "LocalConnections.cs",
            "LogFactory.cs",
            "LogFilter.cs",
            "MessagePacker.cs",
            "Messages.cs",
            "NetworkAuthenticator.cs",
            "NetworkBehaviour.cs",
            "NetworkClient.cs",
            "NetworkConnection.cs",
            "NetworkConnectionToClient.cs",
            "NetworkConnectionToServer.cs",
            "NetworkDiagnostics.cs",
            "NetworkMessage.cs",
            "NetworkReader.cs",
            "NetworkReaderPool.cs",
            "NetworkServer.cs",
            "NetworkTime.cs",
            "NetworkVisibility.cs",
            "NetworkWriter.cs",
            "NetworkWriterPool.cs",
            "RemoteCallHelper.cs",
            "RemoteCallHelper.cs.meta",
            "StringHash.cs",
            "SyncDictionary.cs",
            "SyncList.cs",
            "SyncObject.cs",
            "SyncSet.cs",
            "UNetwork.cs",
        };

        public static void TryDeleteOldScripts(bool showPrompt)
        {
            try
            {
                DeleteOldScripts(showPrompt);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to delete Old Scripts files, Exception:{e.Message}");
            }
        }

        static void DeleteOldScripts(bool showPrompt)
        {
            string basePath = FindBasePath();
            if (string.IsNullOrEmpty(basePath))
            {
                Debug.LogError($"Base path was null or empty, can not delete old scripts");
                return;
            }

            if (ScriptsAlreadyDeleted(basePath))
            {
                return;
            }


            bool shouldDelete = !showPrompt || AskUserIfTheyWantToDelete();
            if (shouldDelete)
            {
                Debug.Log("Automically Cleaning up old script files");
                FindAndDeleteFiles(basePath);
            }
            else
            {
                Debug.LogWarning("Automatic Cleaning up cancelled, empty scripts have not been deleted and need to be manually deleted");

                // delete marker file so that prompt isnt shown every time
                DeleteMarkerFile(basePath);
            }
        }

        static string FindBasePath()
        {
            string path = AssetDatabase.GUIDToAssetPath(RuntimeFolderGuid);
            return path;
        }

        static bool AskUserIfTheyWantToDelete()
        {
            const string title = "Delete Old File?";
            const string message = "Mirror has moved some of the script files in the Runtime folder into sub folders.\n\n" +
                "Unity Packages do not delete old files when they are moved so instead they have to be replaced with empty files\n\n" +
                "Mirror will try to find and delete these old empty files.";
            return EditorUtility.DisplayDialog(title, message, "Ok");
        }

        static bool ScriptsAlreadyDeleted(string basePath)
        {
            string path = Path.Combine(basePath, MarkerFile);

            return !File.Exists(path);
        }

        static void DeleteMarkerFile(string basePath)
        {
            string path = Path.Combine(basePath, MarkerFile);

            File.Delete(path);
        }

        static void FindAndDeleteFiles(string basePath)
        {
            bool anyDeleted = false;
            foreach (string relativePath in oldScripts)
            {
                string path = Path.Combine(basePath, relativePath);

                if (!File.Exists(path))
                {
                    Debug.LogWarning($"File did not exist at path '{path}'");
                    continue;
                }

                string text = File.ReadAllText(path);
                if (SafeToDelete(text))
                {
                    DeleteFile(path);
                    anyDeleted = true;
                }
                else
                {
                    Debug.LogError($"skipping delete because file at path was not empty, path = '{path}'");
                }
            }

            DeleteMarkerFile(basePath);

            if (anyDeleted)
            {
                // call refresh after deleting files
                AssetDatabase.Refresh();
            }
        }

        static bool SafeToDelete(string text)
        {
            return text.Length == 0 || text.Contains("// DELETE ME");
        }

        static void DeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"File.Delete failed to delete asset at '{path}'\nException:{e.Message}");
            }
        }
    }
}
