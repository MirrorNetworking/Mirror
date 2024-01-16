using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using Debug = UnityEngine.Debug;

namespace ParrelSync
{
    /// <summary>
    /// Contains all required methods for creating a linked clone of the Unity project.
    /// </summary>
    public class ClonesManager
    {
        /// <summary>
        /// Name used for an identifying file created in the clone project directory.
        /// </summary>
        /// <remarks>
        /// (!) Do not change this after the clone was created, because then connection will be lost.
        /// </remarks>
        public const string CloneFileName = ".clone";

        /// <summary>
        /// Suffix added to the end of the project clone name when it is created.
        /// </summary>
        /// <remarks>
        /// (!) Do not change this after the clone was created, because then connection will be lost.
        /// </remarks>
        public const string CloneNameSuffix = "_clone";

        public const string ProjectName = "ParrelSync";

        /// <summary>
        /// The maximum number of clones
        /// </summary>
        public const int MaxCloneProjectCount = 10;

        /// <summary>
        /// Name of the file for storing clone's argument.
        /// </summary>
        public const string ArgumentFileName = ".parrelsyncarg";

        /// <summary>
        /// Default argument of the new clone
        /// </summary>
        public const string DefaultArgument = "client";

        #region Managing clones

        /// <summary>
        /// Creates clone from the project currently open in Unity Editor.
        /// </summary>
        /// <returns></returns>
        public static Project CreateCloneFromCurrent()
        {
            if (IsClone())
            {
                Debug.LogError("This project is already a clone. Cannot clone it.");
                return null;
            }

            string currentProjectPath = ClonesManager.GetCurrentProjectPath();
            return ClonesManager.CreateCloneFromPath(currentProjectPath);
        }

        /// <summary>
        /// Creates clone of the project located at the given path.
        /// </summary>
        /// <param name="sourceProjectPath"></param>
        /// <returns></returns>
        public static Project CreateCloneFromPath(string sourceProjectPath)
        {
            Project sourceProject = new Project(sourceProjectPath);

            string cloneProjectPath = null;

            //Find available clone suffix id
            for (int i = 0; i < MaxCloneProjectCount; i++)
            {
                string originalProjectPath = ClonesManager.GetCurrentProject().projectPath;
                string possibleCloneProjectPath = originalProjectPath + ClonesManager.CloneNameSuffix + "_" + i;

                if (!Directory.Exists(possibleCloneProjectPath))
                {
                    cloneProjectPath = possibleCloneProjectPath;
                    break;
                }
            }

            if (string.IsNullOrEmpty(cloneProjectPath))
            {
                Debug.LogError("The number of cloned projects has reach its limit. Limit: " + MaxCloneProjectCount);
                return null;
            }

            Project cloneProject = new Project(cloneProjectPath);

            Debug.Log("Start cloning project, original project: " + sourceProject + ", clone project: " + cloneProject);

            ClonesManager.CreateProjectFolder(cloneProject);

            //Copy Folders           
            Debug.Log("Library copy: " + cloneProject.libraryPath);
            ClonesManager.CopyDirectoryWithProgressBar(sourceProject.libraryPath, cloneProject.libraryPath,
                "Cloning Project Library '" + sourceProject.name + "'. ");
            Debug.Log("Packages copy: " + cloneProject.libraryPath);
            ClonesManager.CopyDirectoryWithProgressBar(sourceProject.packagesPath, cloneProject.packagesPath,
              "Cloning Project Packages '" + sourceProject.name + "'. ");


            //Link Folders
            ClonesManager.LinkFolders(sourceProject.assetPath, cloneProject.assetPath);
            ClonesManager.LinkFolders(sourceProject.projectSettingsPath, cloneProject.projectSettingsPath);
            ClonesManager.LinkFolders(sourceProject.autoBuildPath, cloneProject.autoBuildPath);
            ClonesManager.LinkFolders(sourceProject.localPackages, cloneProject.localPackages);

            ClonesManager.RegisterClone(cloneProject);

            return cloneProject;
        }

        /// <summary>
        /// Registers a clone by placing an identifying ".clone" file in its root directory.
        /// </summary>
        /// <param name="cloneProject"></param>
        private static void RegisterClone(Project cloneProject)
        {
            /// Add clone identifier file.
            string identifierFile = Path.Combine(cloneProject.projectPath, ClonesManager.CloneFileName);
            File.Create(identifierFile).Dispose();

            //Add argument file with default argument
            string argumentFilePath = Path.Combine(cloneProject.projectPath, ClonesManager.ArgumentFileName);
            File.WriteAllText(argumentFilePath, DefaultArgument, System.Text.Encoding.UTF8);

            /// Add collabignore.txt to stop the clone from messing with Unity Collaborate if it's enabled. Just in case.
            string collabignoreFile = Path.Combine(cloneProject.projectPath, "collabignore.txt");
            File.WriteAllText(collabignoreFile, "*"); /// Make it ignore ALL files in the clone.
        }

        /// <summary>
        /// Opens a project located at the given path (if one exists).
        /// </summary>
        /// <param name="projectPath"></param>
        public static void OpenProject(string projectPath)
        {
            if (!Directory.Exists(projectPath))
            {
                Debug.LogError("Cannot open the project - provided folder (" + projectPath + ") does not exist.");
                return;
            }

            if (projectPath == ClonesManager.GetCurrentProjectPath())
            {
                Debug.LogError("Cannot open the project - it is already open.");
                return;
            }

            //Validate (and update if needed) the "Packages" folder before opening clone project to ensure the clone project will have the 
            //same "compiling environment" as the original project
            ValidateCopiedFoldersIntegrity.ValidateFolder(projectPath, GetOriginalProjectPath(), "Packages");

            string fileName = GetApplicationPath();
            string args = "-projectPath \"" + projectPath + "\"";
            Debug.Log("Opening project \"" + fileName + " " + args + "\"");
            ClonesManager.StartHiddenConsoleProcess(fileName, args);
        }

        private static string GetApplicationPath()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    return EditorApplication.applicationPath;
                case RuntimePlatform.OSXEditor:
                    return EditorApplication.applicationPath + "/Contents/MacOS/Unity";
                case RuntimePlatform.LinuxEditor:
                    return EditorApplication.applicationPath;
                default:
                    throw new System.NotImplementedException("Platform has not supported yet ;(");
            }
        }

        /// <summary>
        /// Is this project being opened by an Unity editor?
        /// </summary>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        public static bool IsCloneProjectRunning(string projectPath)
        {

            //Determine whether it is opened in another instance by checking the UnityLockFile
            string UnityLockFilePath = new string[] { projectPath, "Temp", "UnityLockfile" }
                .Aggregate(Path.Combine);

            switch (Application.platform)
            {
                case (RuntimePlatform.WindowsEditor):
                    //Windows editor will lock "UnityLockfile" file when project is being opened.
                    //Sometime, for instance: windows editor crash, the "UnityLockfile" will not be deleted even the project
                    //isn't being opened, so a check to the "UnityLockfile" lock status may be necessary.
                    if (Preferences.AlsoCheckUnityLockFileStaPref.Value)
                        return File.Exists(UnityLockFilePath) && FileUtilities.IsFileLocked(UnityLockFilePath);
                    else
                        return File.Exists(UnityLockFilePath);
                case (RuntimePlatform.OSXEditor):
                    //Mac editor won't lock "UnityLockfile" file when project is being opened
                    return File.Exists(UnityLockFilePath);
                case (RuntimePlatform.LinuxEditor):
                    return File.Exists(UnityLockFilePath);
                default:
                    throw new System.NotImplementedException("IsCloneProjectRunning: Unsupport Platfrom: " + Application.platform);
            }
        }

        /// <summary>
        /// Deletes the clone of the currently open project, if such exists.
        /// </summary>
        public static void DeleteClone(string cloneProjectPath)
        {
            /// Clone won't be able to delete itself.
            if (ClonesManager.IsClone()) return;

            ///Extra precautions.
            if (cloneProjectPath == string.Empty) return;
            if (cloneProjectPath == ClonesManager.GetOriginalProjectPath()) return;

            //Check what OS is
            string identifierFile;
            string args;
            switch (Application.platform)
            {
                case (RuntimePlatform.WindowsEditor):
                    Debug.Log("Attempting to delete folder \"" + cloneProjectPath + "\"");

                    //The argument file will be deleted first at the beginning of the project deletion process 
                    //to prevent any further reading and writing to it(There's a File.Exist() check at the (file)editor windows.)
                    //If there's any file in the directory being write/read during the deletion process, the directory can't be fully removed.
                    identifierFile = Path.Combine(cloneProjectPath, ClonesManager.ArgumentFileName);
                    File.Delete(identifierFile);

                    args = "/c " + @"rmdir /s/q " + string.Format("\"{0}\"", cloneProjectPath);
                    StartHiddenConsoleProcess("cmd.exe", args);

                    break;
                case (RuntimePlatform.OSXEditor):
                    Debug.Log("Attempting to delete folder \"" + cloneProjectPath + "\"");

                    //The argument file will be deleted first at the beginning of the project deletion process 
                    //to prevent any further reading and writing to it(There's a File.Exist() check at the (file)editor windows.)
                    //If there's any file in the directory being write/read during the deletion process, the directory can't be fully removed.
                    identifierFile = Path.Combine(cloneProjectPath, ClonesManager.ArgumentFileName);
                    File.Delete(identifierFile);

                    FileUtil.DeleteFileOrDirectory(cloneProjectPath);

                    break;
                case (RuntimePlatform.LinuxEditor):
                    Debug.Log("Attempting to delete folder \"" + cloneProjectPath + "\"");
                    identifierFile = Path.Combine(cloneProjectPath, ClonesManager.ArgumentFileName);
                    File.Delete(identifierFile);

                    FileUtil.DeleteFileOrDirectory(cloneProjectPath);

                    break;
                default:
                    Debug.LogWarning("Not in a known editor. Where are you!?");
                    break;
            }
        }

        #endregion

        #region Creating project folders

        /// <summary>
        /// Creates an empty folder using data in the given Project object
        /// </summary>
        /// <param name="project"></param>
        public static void CreateProjectFolder(Project project)
        {
            string path = project.projectPath;
            Debug.Log("Creating new empty folder at: " + path);
            Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Copies the full contents of the unity library. We want to do this to avoid the lengthy re-serialization of the whole project when it opens up the clone.
        /// </summary>
        /// <param name="sourceProject"></param>
        /// <param name="destinationProject"></param>
        [System.Obsolete]
        public static void CopyLibraryFolder(Project sourceProject, Project destinationProject)
        {
            if (Directory.Exists(destinationProject.libraryPath))
            {
                Debug.LogWarning("Library copy: destination path already exists! ");
                return;
            }

            Debug.Log("Library copy: " + destinationProject.libraryPath);
            ClonesManager.CopyDirectoryWithProgressBar(sourceProject.libraryPath, destinationProject.libraryPath,
                "Cloning project '" + sourceProject.name + "'. ");
        }

        #endregion

        #region Creating symlinks

        /// <summary>
        /// Creates a symlink between destinationPath and sourcePath (Mac version).
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        private static void CreateLinkMac(string sourcePath, string destinationPath)
        {
            sourcePath = sourcePath.Replace(" ", "\\ ");
            destinationPath = destinationPath.Replace(" ", "\\ ");
            var command = string.Format("ln -s {0} {1}", sourcePath, destinationPath);

            Debug.Log("Mac hard link " + command);

            ClonesManager.ExecuteBashCommand(command);
        }

        /// <summary>
        /// Creates a symlink between destinationPath and sourcePath (Linux version).
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        private static void CreateLinkLinux(string sourcePath, string destinationPath)
        {
            sourcePath = sourcePath.Replace(" ", "\\ ");
            destinationPath = destinationPath.Replace(" ", "\\ ");
            var command = string.Format("ln -s {0} {1}", sourcePath, destinationPath);           

            Debug.Log("Linux Symlink " + command);

            ClonesManager.ExecuteBashCommand(command);
        }

        /// <summary>
        /// Creates a symlink between destinationPath and sourcePath (Windows version).
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        private static void CreateLinkWin(string sourcePath, string destinationPath)
        {
            string cmd = "/C mklink /J " + string.Format("\"{0}\" \"{1}\"", destinationPath, sourcePath);
            Debug.Log("Windows junction: " + cmd);
            ClonesManager.StartHiddenConsoleProcess("cmd.exe", cmd);
        }

        //TODO(?) avoid terminal calls and use proper api stuff. See below for windows! 
        ////https://docs.microsoft.com/en-us/windows/desktop/api/ioapiset/nf-ioapiset-deviceiocontrol
        //[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //private static extern bool DeviceIoControl(System.IntPtr hDevice, uint dwIoControlCode,
        //	System.IntPtr InBuffer, int nInBufferSize,
        //	System.IntPtr OutBuffer, int nOutBufferSize,
        //	out int pBytesReturned, System.IntPtr lpOverlapped);

        /// <summary>
        /// Create a link / junction from the original project to it's clone.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        public static void LinkFolders(string sourcePath, string destinationPath)
        {
            if ((Directory.Exists(destinationPath) == false) && (Directory.Exists(sourcePath) == true))
            {
                switch (Application.platform)
                {
                    case (RuntimePlatform.WindowsEditor):
                        CreateLinkWin(sourcePath, destinationPath);
                        break;
                    case (RuntimePlatform.OSXEditor):
                        CreateLinkMac(sourcePath, destinationPath);
                        break;
                    case (RuntimePlatform.LinuxEditor):
                        CreateLinkLinux(sourcePath, destinationPath);
                        break;
                    default:
                        Debug.LogWarning("Not in a known editor. Application.platform: " + Application.platform);
                        break;
                }
            }
            else
            {
                Debug.LogWarning("Skipping Asset link, it already exists: " + destinationPath);
            }
        }

        #endregion

        #region Utility methods

        private static bool? isCloneFileExistCache = null;

        /// <summary>
        /// Returns true if the project currently open in Unity Editor is a clone.
        /// </summary>
        /// <returns></returns>
        public static bool IsClone()
        {
            if (isCloneFileExistCache == null)
            {
                /// The project is a clone if its root directory contains an empty file named ".clone".
                string cloneFilePath = Path.Combine(ClonesManager.GetCurrentProjectPath(), ClonesManager.CloneFileName);
                isCloneFileExistCache = File.Exists(cloneFilePath);
            }

            return (bool)isCloneFileExistCache;
        }

        /// <summary>
        /// Get the path to the current unityEditor project folder's info
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentProjectPath()
        {
            return Application.dataPath.Replace("/Assets", "");
        }

        /// <summary>
        /// Return a project object that describes all the paths we need to clone it.
        /// </summary>
        /// <returns></returns>
        public static Project GetCurrentProject()
        {
            string pathString = ClonesManager.GetCurrentProjectPath();
            return new Project(pathString);
        }

        /// <summary>
        /// Get the argument of this clone project.
        /// If this is the original project, will return an empty string.
        /// </summary>
        /// <returns></returns>
        public static string GetArgument()
        {
            string argument = "";
            if (IsClone())
            {
                string argumentFilePath = Path.Combine(GetCurrentProjectPath(), ClonesManager.ArgumentFileName);
                if (File.Exists(argumentFilePath))
                {
                    argument = File.ReadAllText(argumentFilePath, System.Text.Encoding.UTF8);
                }
            }

            return argument;
        }

        /// <summary>
        /// Returns the path to the original project.
        /// If currently open project is the original, returns its own path.
        /// If the original project folder cannot be found, retuns an empty string.
        /// </summary>
        /// <returns></returns>
        public static string GetOriginalProjectPath()
        {
            if (IsClone())
            {
                /// If this is a clone...
                /// Original project path can be deduced by removing the suffix from the clone's path.
                string cloneProjectPath = ClonesManager.GetCurrentProject().projectPath;

                int index = cloneProjectPath.LastIndexOf(ClonesManager.CloneNameSuffix);
                if (index > 0)
                {
                    string originalProjectPath = cloneProjectPath.Substring(0, index);
                    if (Directory.Exists(originalProjectPath)) return originalProjectPath;
                }

                return string.Empty;
            }
            else
            {
                /// If this is the original, we return its own path.
                return ClonesManager.GetCurrentProjectPath();
            }
        }

        /// <summary>
        /// Returns all clone projects path.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetCloneProjectsPath()
        {
            List<string> projectsPath = new List<string>();
            for (int i = 0; i < MaxCloneProjectCount; i++)
            {
                string originalProjectPath = ClonesManager.GetCurrentProject().projectPath;
                string cloneProjectPath = originalProjectPath + ClonesManager.CloneNameSuffix + "_" + i;

                if (Directory.Exists(cloneProjectPath))
                    projectsPath.Add(cloneProjectPath);
            }

            return projectsPath;
        }

        /// <summary>
        /// Copies directory located at sourcePath to destinationPath. Displays a progress bar.
        /// </summary>
        /// <param name="source">Directory to be copied.</param>
        /// <param name="destination">Destination directory (created automatically if needed).</param>
        /// <param name="progressBarPrefix">Optional string added to the beginning of the progress bar window header.</param>
        public static void CopyDirectoryWithProgressBar(string sourcePath, string destinationPath,
            string progressBarPrefix = "")
        {
            var source = new DirectoryInfo(sourcePath);
            var destination = new DirectoryInfo(destinationPath);

            long totalBytes = 0;
            long copiedBytes = 0;

            ClonesManager.CopyDirectoryWithProgressBarRecursive(source, destination, ref totalBytes, ref copiedBytes,
                progressBarPrefix);
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Copies directory located at sourcePath to destinationPath. Displays a progress bar.
        /// Same as the previous method, but uses recursion to copy all nested folders as well.
        /// </summary>
        /// <param name="source">Directory to be copied.</param>
        /// <param name="destination">Destination directory (created automatically if needed).</param>
        /// <param name="totalBytes">Total bytes to be copied. Calculated automatically, initialize at 0.</param>
        /// <param name="copiedBytes">To track already copied bytes. Calculated automatically, initialize at 0.</param>
        /// <param name="progressBarPrefix">Optional string added to the beginning of the progress bar window header.</param>
        private static void CopyDirectoryWithProgressBarRecursive(DirectoryInfo source, DirectoryInfo destination,
            ref long totalBytes, ref long copiedBytes, string progressBarPrefix = "")
        {
            /// Directory cannot be copied into itself.
            if (source.FullName.ToLower() == destination.FullName.ToLower())
            {
                Debug.LogError("Cannot copy directory into itself.");
                return;
            }

            /// Calculate total bytes, if required.
            if (totalBytes == 0)
            {
                totalBytes = ClonesManager.GetDirectorySize(source, true, progressBarPrefix);
            }

            /// Create destination directory, if required.
            if (!Directory.Exists(destination.FullName))
            {
                Directory.CreateDirectory(destination.FullName);
            }

            /// Copy all files from the source.
            foreach (FileInfo file in source.GetFiles())
            {
                try
                {
                    file.CopyTo(Path.Combine(destination.ToString(), file.Name), true);
                }
                catch (IOException)
                {
                    /// Some files may throw IOException if they are currently open in Unity editor.
                    /// Just ignore them in such case.
                }

                /// Account the copied file size.
                copiedBytes += file.Length;

                /// Display the progress bar.
                float progress = (float)copiedBytes / (float)totalBytes;
                bool cancelCopy = EditorUtility.DisplayCancelableProgressBar(
                    progressBarPrefix + "Copying '" + source.FullName + "' to '" + destination.FullName + "'...",
                    "(" + (progress * 100f).ToString("F2") + "%) Copying file '" + file.Name + "'...",
                    progress);
                if (cancelCopy) return;
            }

            /// Copy all nested directories from the source.
            foreach (DirectoryInfo sourceNestedDir in source.GetDirectories())
            {
                DirectoryInfo nextDestingationNestedDir = destination.CreateSubdirectory(sourceNestedDir.Name);
                ClonesManager.CopyDirectoryWithProgressBarRecursive(sourceNestedDir, nextDestingationNestedDir,
                    ref totalBytes, ref copiedBytes, progressBarPrefix);
            }
        }

        /// <summary>
        /// Calculates the size of the given directory. Displays a progress bar.
        /// </summary>
        /// <param name="directory">Directory, which size has to be calculated.</param>
        /// <param name="includeNested">If true, size will include all nested directories.</param>
        /// <param name="progressBarPrefix">Optional string added to the beginning of the progress bar window header.</param>
        /// <returns>Size of the directory in bytes.</returns>
        private static long GetDirectorySize(DirectoryInfo directory, bool includeNested = false,
            string progressBarPrefix = "")
        {
            EditorUtility.DisplayProgressBar(progressBarPrefix + "Calculating size of directories...",
                "Scanning '" + directory.FullName + "'...", 0f);

            /// Calculate size of all files in directory.
            long filesSize = directory.GetFiles().Sum((FileInfo file) => file.Length);

            /// Calculate size of all nested directories.
            long directoriesSize = 0;
            if (includeNested)
            {
                IEnumerable<DirectoryInfo> nestedDirectories = directory.GetDirectories();
                foreach (DirectoryInfo nestedDir in nestedDirectories)
                {
                    directoriesSize += ClonesManager.GetDirectorySize(nestedDir, true, progressBarPrefix);
                }
            }

            return filesSize + directoriesSize;
        }

        /// <summary>
        /// Starts process in the system console, taking the given fileName and args.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="args"></param>
        private static void StartHiddenConsoleProcess(string fileName, string args)
        {
            System.Diagnostics.Process.Start(fileName, args);
        }

        /// <summary>
        /// Thanks to https://github.com/karl-/unity-symlink-utility/blob/master/SymlinkUtility.cs
        /// </summary>
        /// <param name="command"></param>
        private static void ExecuteBashCommand(string command)
        {
            command = command.Replace("\"", "\"\"");

            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"" + command + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            using (proc)
            {
                proc.Start();
                proc.WaitForExit();

                if (!proc.StandardError.EndOfStream)
                {
                    UnityEngine.Debug.LogError(proc.StandardError.ReadToEnd());
                }
            }
        }

        public static void OpenProjectInFileExplorer(string path)
        {
            System.Diagnostics.Process.Start(@path);
        }
        #endregion
    }
}
