// Mirror Network Migration Tool
// Written by M. Coburn (@coburn64 on Twitter/SoftwareGuy on Github)
// This file is part of Mirror Networking by Coburn64, Lymdun, vis2k and Paul (goldbug).
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;

// This is an editor script and should not be referenced in Runtime binaries.
namespace Mirror
{
    /// <summary>
    /// A migration tool that will help migrate stock UNET code to Mirror code.
    /// </summary>
    public static class MirrorNetworkMigrator
    {
        // Private variables that don't need to be modified.
        static string scriptExtension = "*.cs";

        static string[] knownIncompatibleRegexes = {
            @"\[Command([^\],]*)\]",
            @"\[ClientRpc([^\],]*)\]",
            @"\[TargetRpc([^\],]*)\]",
            @"\[SyncEvent([^\],]*)\]"
        };

        static string[] knownCompatibleReplacements = {
            "[Command]",
            "[ClientRpc]",
            "[TargetRpc]",
            "[SyncEvent]"
        };

        static int filesModified = 0;
        static string scriptBuffer = string.Empty;
        static MatchCollection matches;

        // Logic portion begins below.

        [MenuItem("Tools/Mirror/UNET Migration Tool")]
        public static void Mirror_Migration_Tool()
        {
            // Safeguard in case a developer goofs up
            if (knownIncompatibleRegexes.Length != knownCompatibleReplacements.Length)
            {
                Debug.Log("[Mirror Migration Tool] BUG DETECTED: Regexes to search for DO NOT match the Regex Replacements. Cannot continue.");
                return;
            }

            // Display a welcome dialog.
            if (EditorUtility.DisplayDialog("Mirror Network Migration Tool", "Welcome to the Migration Tool for Mirror Networking. " +
                "This tool will convert your existing UNET code into the Mirror equivalent code.\n\nBefore we begin, we STRONGLY " +
                "recommend you take a full backup of your project as this tool is not perfect.\n\nWhile it does not attempt to " +
                "purposefully trash your network scripts, it could break your project. Be smart and BACKUP NOW.", 
                "I'm good.", "I'll backup first.")) {

                // User accepted the risks - go ahead!
                MigrationTool_DoActualMigration();
                AssetDatabase.Refresh();
            } else {
                EditorUtility.DisplayDialog("Aborted", "You opted to abort the migration process. Please come back once you've taken a backup.", "Got it");
                return;
            }
        }

        private static void MigrationTool_DoActualMigration()
        {
            // Place holder for the assets folder location.
            string assetsFolder = Application.dataPath;
            // List structure for the CSharp files.
            List<string> filesToScanAndModify = new List<string>();

            // Be verbose and say what's happening.
            Debug.Log("[Mirror Migration Tool] Determined your asset folder is at: " + assetsFolder);
            Debug.Log("[Mirror Migration Tool] Scanning your C# scripts... This might take a moment.");

            // Now we scan the directory...
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(assetsFolder);

                // For every entry in this structure add it to the list.
                // SearchOption.AllDirectories will traverse the directory stack
                foreach (FileInfo potentialFile in dirInfo.GetFiles(scriptExtension, SearchOption.AllDirectories))
                {
                    // DEBUG ONLY. This will cause massive Unity Console Spammage!
                    Debug.Log("[Mirror Migration Tool] DEBUG: Scanned " + potentialFile.FullName);
                    filesToScanAndModify.Add(potentialFile.FullName);
                }

                // Final chance to abort.
                if (!EditorUtility.DisplayDialog("Continue?", string.Format("We've found {0} file(s) that may need updating. Depending on your hardware and storage, " +
                    "this might take a while. Do you wish to continue the process?", filesToScanAndModify.Count), "Go ahead!", "Abort"))
                {
                    EditorUtility.DisplayDialog("Aborted", "You opted to abort the migration process. Please come back once you're ready to migrate.", "Got it");
                    return;
                }

                // Okay, let's do this!
                MigrationTool_FileProcessor(filesToScanAndModify);

                Debug.Log("[Mirror Migration Tool] Processed " + filesModified + " files");

                EditorUtility.DisplayDialog("Migration complete.", "Congratulations, you should now be Mirror Network ready.\n\n" +
                    "Thanks for using Mirror and Telepathy Networking Stack for Unity!\n\nPlease don't forget to drop by the GitHub repository to keep up to date and the Discord server if you have any problems. Have fun!", "Awesome");

                filesModified = 0;
                return;

            } catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Oh no!", "An exception occurred. If you think this is a Mirror Networking bug, please file a bug report on the GitHub repository. " +
                    "I'll now tell you what I encountered:\n\n" + ex.ToString(), "Okay");         
                return;
            }
        }

        private static void MigrationTool_FileProcessor(List<string> filesToProcess)
        {
            StreamReader sr;
            StreamWriter sw;

            foreach(string file in filesToProcess)
            {
                try
                {
                    // Open and load it into the script buffer.
                    using(sr = new StreamReader(file))
                    {
                        scriptBuffer = sr.ReadToEnd();
                        sr.Close();
                    }

                    // Get outta here, UnityEngine.Networking !
                    scriptBuffer = scriptBuffer.Replace("using UnityEngine.Networking;", "using Mirror;");

                    // Work our magic.
                    for (int i = 0; i < knownIncompatibleRegexes.Length; i++)
                    {
                        matches = Regex.Matches(scriptBuffer, knownIncompatibleRegexes[i]);
                        if (matches.Count > 0)
                        {
                            // It was successful - replace it.
                            scriptBuffer = Regex.Replace(scriptBuffer, knownIncompatibleRegexes[i], knownCompatibleReplacements[i]);
                        }
                    }

                    // Be extra gentle with some like NetworkSettings directives.
                    matches = Regex.Matches(scriptBuffer, @"NetworkSettings\(([^\)]*)\)");
                    // A file could have more than one NetworkSettings... better to just do the whole lot.
                    // We don't know what the developer might be doing.
                    if (matches.Count > 0) {
                        for (int i = 0; i < matches.Count; i++)
                        {
                            Match nsm = Regex.Match(matches[i].ToString(), @"(?<=\().+?(?=\))");
                            if (nsm.Success)
                            {
                                string[] netSettingArguments = nsm.ToString().Split(',');
                                if (netSettingArguments.Length > 1)
                                {
                                    string patchedNetSettings = string.Empty;

                                    int a = 0;
                                    foreach (string argument in netSettingArguments)
                                    {
                                        // Increment a, because that's how many elements we've looked at.
                                        a++;

                                        // If it contains the offender, just continue, don't do anything.
                                        if (argument.Contains("channel")) continue;

                                        // If it doesn't then add it to our new string.
                                        patchedNetSettings += argument.Trim();
                                        if (a < netSettingArguments.Length) patchedNetSettings += ", ";
                                    }

                                    // a = netSettingArguments.Length; patch it up and there we go.
                                    scriptBuffer = Regex.Replace(scriptBuffer, nsm.Value, patchedNetSettings);
                                } else {
                                    // Replace it.
                                    if (netSettingArguments[0].Contains("channel"))
                                    {
                                        // Don't touch this.
                                        scriptBuffer = scriptBuffer.Replace(string.Format("[{0}]", matches[i].Value), string.Empty);
                                    }
                                    // DONE!
                                }
                            }
                        }
                    }

                    // Backup the old files for safety.
                    // The user can delete them later.
                    if(!File.Exists(file + ".bak")) File.Copy(file, file + ".bak");

                    // Now the job is done, we want to write the data out to disk... 
                    using(sw = new StreamWriter(file, false, Encoding.UTF8))
                    {
                        sw.WriteLine(scriptBuffer);
                        sw.Close();
                    }

                    filesModified++;
                } catch (System.Exception e) {
                    Debug.LogError(string.Format("[Mirror Migration Tool] Encountered an exception processing {0}:\n{1}", file, e.ToString()));
                }
            }
        }
    }
}
