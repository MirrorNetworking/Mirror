using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mirror.EditorScripts
{
    public static class CleanupOldScripts
    {
        const string RuntimeFolderGuid = "9f4328ccc5f724e45afe2215d275b5d5";

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

        public static void DeleteOldScripts()
        {
            string basePath = FindBasePath();
            if (string.IsNullOrEmpty(basePath))
            {
                Debug.LogError($"Base path was null or empty, can not delete old scripts");
                return;
            }

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
                if (text.Contains("// DELETE ME"))
                {
                    DeleteFile(path);
                    anyDeleted = true;
                }
                else
                {
                    Debug.LogError($"skipping delete because file at path was not empty, path = '{path}'");
                }
            }


            if (anyDeleted)
            {
                // call refresh after deleting files
                AssetDatabase.Refresh();
            }
        }

        static string FindBasePath()
        {
            string path = AssetDatabase.GUIDToAssetPath(RuntimeFolderGuid);
            return path;
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
