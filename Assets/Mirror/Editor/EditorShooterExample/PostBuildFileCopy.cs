using System.IO;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor;

/// <summary>
/// Copies files to Build folder after build completes
/// </summary>
/// <remarks>Credit: JesusLuvsYooh StephenAllenGames.co.uk && MrGadget for this scripts overhaul</remarks>
public class PostBuildFileCopy : IPostprocessBuildWithReport
{
    // use the format of  "folder/file.bat" or "file.bat"
    string[] filePaths = new string[]
    {
//#if !UNITY_SERVER
        "Mirror/Editor/EditorShooterExample/ServerArgs.bat",
        "Mirror/Editor/EditorShooterExample/ClientArgs.bat"
//#endif
    };

    string outputFileName;
    string inputPath;
    string outputPath;

    // required by the IPostprocessBuildWithReport
    public int callbackOrder { get { return 0; } }

    public void OnPostprocessBuild(BuildReport report)
    {
        Debug.Log($"OnPostprocessBuild for target {report.summary.platform} at path {report.summary.outputPath}");

        // Handle special cases for various build platforms.
        if (report.summary.platform == BuildTarget.WebGL)
            outputPath = Path.GetDirectoryName(report.summary.outputPath);

        if (report.summary.platform == BuildTarget.StandaloneWindows64)
            outputPath = Path.GetDirectoryName(report.summary.outputPath);

        if (report.summary.platform == BuildTarget.StandaloneOSX)
            outputPath = Path.Combine(report.summary.outputPath);
        // on older Macs: outputPath = Path.Combine(report.summary.outputPath, "Contents");

        if (report.summary.platform == BuildTarget.StandaloneLinux64)
            outputPath = Path.Combine(outputPath, "_Data");

//#if !UNITY_STANDALONE_OSX || !UNITY_EDITOR_OSX
        foreach (string _value in filePaths)
        {
            // Application.dataPath should be Assets folder in Unity Editor
            inputPath = Path.Combine(Application.dataPath, _value);

            // Automated way to separate filePaths listed in the array, from the final output file name
            outputFileName = Path.GetFileName(_value);
            outputFileName = Path.Combine(outputPath, outputFileName);

            // Unity pops up a Try again or Force Close box, if Copy is called with no existing file, add a check.
            if (!File.Exists(inputPath))
            {
                Debug.LogWarning($"File not found: {inputPath}");
            }
            else
            {
                Debug.Log($"Copying {inputPath} to {outputFileName}");
                File.Copy(inputPath, outputFileName, true);
            }
        }
//#endif
    }
}
