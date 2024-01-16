using System;
using UnityEditor;
using UnityEngine;
namespace ParrelSync.Update
{
    /// <summary>
    /// A simple update checker
    /// </summary>
    public class UpdateChecker
    {
        //const string LocalVersionFilePath = "Assets/ParrelSync/VERSION.txt";
        public const string LocalVersion = "1.5.1";
        [MenuItem("ParrelSync/Check for update", priority = 20)]
        static void CheckForUpdate()
        {
            using (System.Net.WebClient client = new System.Net.WebClient())
            {
                try
                {
                    //This won't work with UPM packages
                    //string localVersionText = AssetDatabase.LoadAssetAtPath<TextAsset>(LocalVersionFilePath).text;

                    string localVersionText = LocalVersion;
                    Debug.Log("Local version text : " + LocalVersion);

                    string latesteVersionText = client.DownloadString(ExternalLinks.RemoteVersionURL);
                    Debug.Log("latest version text got: " + latesteVersionText);
                    string messageBody = "Current Version: " + localVersionText +"\n"
                                         +"Latest Version: " + latesteVersionText + "\n";
                    var latestVersion = new Version(latesteVersionText);
                    var localVersion = new Version(localVersionText);

                    if (latestVersion > localVersion)
                    {
                        Debug.Log("There's a newer version");
                        messageBody += "There's a newer version available";
                        if(EditorUtility.DisplayDialog("Check for update.", messageBody, "Get latest release", "Close"))
                        {
                            Application.OpenURL(ExternalLinks.Releases);
                        }
                    }
                    else
                    {
                        Debug.Log("Current version is up-to-date.");
                        messageBody += "Current version is up-to-date.";
                        EditorUtility.DisplayDialog("Check for update.", messageBody,"OK");
                    }
                    
                }
                catch (Exception exp)
                {
                    Debug.LogError("Error with checking update. Exception: " + exp);
                    EditorUtility.DisplayDialog("Update Error","Error with checking update. \nSee console for more details.",
                     "OK"
                    );
                }
            }
        }
    }
}
