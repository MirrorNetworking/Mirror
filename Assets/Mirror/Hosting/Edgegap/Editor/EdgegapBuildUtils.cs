#if UNITY_EDITOR
using Edgegap.Editor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Edgegap
{
    internal static class EdgegapBuildUtils
    {
        public static bool IsLogLevelDebug =>
            EdgegapWindowMetadata.LOG_LEVEL == EdgegapWindowMetadata.LogLevel.Debug;
        public static bool IsArmCPU() =>
            RuntimeInformation.ProcessArchitecture == Architecture.Arm ||
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public static BuildReport BuildServer(string folderName)
        {
            IEnumerable<string> scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                target = BuildTarget.StandaloneLinux64,
                // MIRROR CHANGE
#if UNITY_2021_3_OR_NEWER
                subtarget = (int)StandaloneBuildSubtarget.Server, // dedicated server with UNITY_SERVER define
#else
                options = BuildOptions.EnableHeadlessMode, // obsolete and missing UNITY_SERVER define
#endif
                // END MIRROR CHANGE
                locationPathName = $"Builds/{folderName}/ServerBuild"
            };

            return BuildPipeline.BuildPlayer(options);
        }

        public static async Task<string> DockerSetupAndInstallationCheck(string path)
        {
            if (!File.Exists(path))
            {
                throw new Exception("Dockerfile not found, please notify plugin maintainer about this issue.");
            }

            string output = null;
            string error = null;
            await RunCommand_DockerVersion(msg => output = msg,
                (msg) =>
                {
                    if (msg.ToLowerInvariant().Contains("error") || msg.ToLowerInvariant().Contains("invalid"))
                    {
                        error = msg;
                    }
                });

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error);
                return error;
            }

            Debug.Log($"[Edgegap] Docker version detected: {output}"); // MIRROR CHANGE

            await RunCommand_DockerPS(null,
                (msg) =>
                {
                    if (msg.ToLowerInvariant().Contains("error") || msg.ToLowerInvariant().Contains("invalid"))
                    {
                        error = msg;
                    }
                });

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error);
                return error;
            }

            return null;
        }

        public static async Task InstallLinuxModules(string unityVersion, Action<string> outputReciever = null, Action<string> errorReciever = null)
        {
            await RunCommand_InstallLinuxRequirements("linux-mono", unityVersion, outputReciever);
            await RunCommand_InstallLinuxRequirements("linux-il2cpp", unityVersion, outputReciever);
        }

        static async Task RunCommand_DockerPS(Action<string> outputReciever = null, Action<string> errorReciever = null)
        {
#if UNITY_EDITOR_WIN
            await RunCommand("cmd.exe", "/c docker ps -q", outputReciever, errorReciever);
#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash", "-c \"docker ps -q\"", outputReciever, errorReciever);
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", "-c \"docker ps -q\"", outputReciever, errorReciever);
#else
            Debug.LogError("The platform is not supported yet.");
#endif
        }

        // MIRROR CHANGE
        static async Task RunCommand_DockerVersion(Action<string> outputReciever = null, Action<string> errorReciever = null)
        {
#if UNITY_EDITOR_WIN
            await RunCommand("cmd.exe", "/c docker --version", outputReciever, errorReciever);
#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash", "-c \"docker --version\"", outputReciever, errorReciever);
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", "-c \"docker --version\"", outputReciever, errorReciever);
#else
            Debug.LogError("The platform is not supported yet.");
#endif
        }

        public static async Task RunCommand_DockerImage(Action<string> outputReciever, Action<string> errorReciever)
        {
#if UNITY_EDITOR_WIN
            await RunCommand("cmd.exe", "/c docker image ls --format \"{{.Repository}}:{{.Tag}}\"", outputReciever,

#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash", "-c \"docker image ls --format \"{{.Repository}}:{{.Tag}}\"\"", outputReciever, 
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", "-c \"docker image ls --format \"{{.Repository}}:{{.Tag}}\"\"", outputReciever, 
#endif
                (msg) =>
                {
                    if (msg.ToLowerInvariant().Contains("error") || msg.ToLowerInvariant().Contains("invalid"))
                    {
                        errorReciever(msg);
                    }
                });
        }

        public static async Task RunCommand_DockerRun(string image, string extraParams)
        {
            // ARM -> x86 support:
            string runCommand = IsArmCPU() ? "run --platform linux/amd64" : "run";

#if UNITY_EDITOR_WIN
            await RunCommand("docker.exe", $"{runCommand} --name edgegap-server-test -d {extraParams} {image}",
#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash", $"-c \"docker {runCommand} --name edgegap-server-test -d {extraParams} {image}\"",
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", $"-c \"docker {runCommand} --name edgegap-server-test -d {extraParams} {image}\"",
#endif
                null,
                (msg) =>
                {
                    if (msg.ToLowerInvariant().Contains("error") || msg.ToLowerInvariant().Contains("invalid"))
                    {
                        throw new Exception(msg);
                    }
                });
        }

        public static async Task RunCommand_DockerStop()
        {
            //Stopping running container
#if UNITY_EDITOR_WIN
            await RunCommand("docker.exe", $"stop edgegap-server-test",
#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash", $"-c \"docker stop edgegap-server-test\"",
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", $"-c \"docker stop edgegap-server-test\"",
#endif
                null,
                (msg) =>
                {
                    if (msg.ToLowerInvariant().Contains("error") || msg.ToLowerInvariant().Contains("invalid"))
                    {
                        throw new Exception(msg);
                    }
                });

            //Deleting the stopped container
#if UNITY_EDITOR_WIN
            await RunCommand("docker.exe", $"rm edgegap-server-test",
#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash", $"-c \"docker rm edgegap-server-test\"",
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", $"-c \"docker rm edgegap-server-test\"",
#endif
                null,
                (msg) =>
                {
                    if (msg.ToLowerInvariant().Contains("error") || msg.ToLowerInvariant().Contains("invalid"))
                    {
                        throw new Exception(msg);
                    }
                });
        }

        static async Task RunCommand_InstallLinuxRequirements(string module, string unityVersion, Action<string> outputReciever = null, Action<string> errorReciever = null)
        {
            string error = null;
#if UNITY_EDITOR_WIN
            await RunCommand("cmd.exe",
                $"\"C:\\Program Files\\Unity Hub\\Unity Hub.exe\" -- --headless install-modules --version {unityVersion} -m {module}",
                outputReciever,
#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash",
                $"/Applications/Unity/Hub.app/Contents/MacOS/Unity/Hub -- --headless install-modules --version {unityVersion} -m linux-mono linux-il2cpp",
                outputReciever,
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash",
                $"~/Applications/Unity/Hub.AppImage --headless install-modules --version {unityVersion} -m linux-mono linux-il2cpp",
                outputReciever,
#endif
            (msg) =>
            {
                if (msg.ToLowerInvariant().Contains("error") || msg.ToLowerInvariant().Contains("invalid"))
                {
                    error = msg;
                }
                outputReciever(msg);
            });

            if (error != null)
            {
                errorReciever(error);
            }
        }

        public static async Task RunCommand_DockerBuild(string dockerfilePath, string registry, string imageRepo, string tag, string projectPath, Action<string> onStatusUpdate, string extraParams = null)
        {
            string realErrorMessage = null;

            // ARM -> x86 support:
            // build commands use 'buildx' on ARM cpus for cross compilation.
            // otherwise docker builds would not launch when deployed because
            // Edgegap's infrastructure is on x86. instead the deployment logs
            // would show an error in a linux .go file with 'not found'.
            string buildCommand = IsArmCPU() ? "buildx build --platform linux/amd64" : "build";

            if (!string.IsNullOrEmpty(extraParams))
            {
                buildCommand += $" {extraParams}";
            }

            bool done = false;

#if UNITY_EDITOR_WIN
            await RunCommand("docker.exe", $"{buildCommand} -f \"{dockerfilePath}\" -t \"{registry}/{imageRepo}:{tag}\" \"{projectPath}\"", onStatusUpdate,
#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash", $"-c \"docker {buildCommand} -f {dockerfilePath} -t {registry}/{imageRepo}:{tag} {projectPath}\"", onStatusUpdate,
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", $"-c \"docker {buildCommand} -f {dockerfilePath} -t {registry}/{imageRepo}:{tag} {projectPath}\"", onStatusUpdate,
#endif
                (msg) =>
                {
                    if (msg.ToLowerInvariant().Contains("error") || msg.ToLowerInvariant().Contains("invalid"))
                    {
                        realErrorMessage = msg;
                    }
                    if (msg.ToLowerInvariant().Contains("done"))
                    {
                        done = true;
                    }
                    Debug.Log(msg);
                    onStatusUpdate(msg);
                });

            if (realErrorMessage != null)
            {
                throw new Exception(realErrorMessage);
            }
            else if (!done)
            {
                throw new Exception("Couldn't complete containerization, see console log for details.");
            }
        }

        public static async Task<string> RunCommand_DockerPush(string registry, string imageRepo, string tag, Action<string> onStatusUpdate)
        {
            string error = null;
#if UNITY_EDITOR_WIN
            await RunCommand("docker.exe", $"push {registry}/{imageRepo}:{tag}", onStatusUpdate,
#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash", $"-c \"docker push {registry}/{imageRepo}:{tag}\"", onStatusUpdate,
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", $"-c \"docker push {registry}/{imageRepo}:{tag}\"", onStatusUpdate,
#endif
            (msg) => error += msg + "\n");

            return error ?? "";
        }

        static async Task RunCommand(string command, string arguments, Action<string> outputReciever = null, Action<string> errorReciever = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

#if !UNITY_EDITOR_WIN
            // on mac, commands like 'docker' aren't found because it's not in the application's PATH
            // even if it runs on mac's terminal.
            // to solve this we need to do two steps:
            // 1. add /usr/bin/local to PATH if it's not there already. often this is missing in the application.
            //    this is where docker is usually instaled.
            // 2. add PATH to ProcessStartInfo
            string existingPath = Environment.GetEnvironmentVariable("PATH");
            string customPath = $"{existingPath}:/usr/local/bin";
            startInfo.EnvironmentVariables["PATH"] = customPath;
            // Debug.Log("PATH: " + customPath);
#endif

            Process proc = new Process() { StartInfo = startInfo, };
            proc.EnableRaisingEvents = true;

            ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
            ConcurrentQueue<string> outputs = new ConcurrentQueue<string>();

            void pipeQueue(ConcurrentQueue<string> q, Action<string> opt)
            {
                while (!q.IsEmpty)
                {
                    if (q.TryDequeue(out string msg) && !string.IsNullOrWhiteSpace(msg))
                    {
                        opt?.Invoke(msg);
                    }
                }
            }

            proc.OutputDataReceived += (s, e) => outputs.Enqueue(e.Data);
            proc.ErrorDataReceived += (s, e) => errors.Enqueue(e.Data);

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            while (!proc.HasExited)
            {
                await Task.Delay(100);
                pipeQueue(errors, errorReciever);
                pipeQueue(outputs, outputReciever);
            }

            pipeQueue(errors, errorReciever);
            pipeQueue(outputs, outputReciever);


        }

        static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        static Regex lastDigitsRegex = new Regex("([0-9])+$");

        public static string IncrementTag(string tag)
        {
            Match lastDigits = lastDigitsRegex.Match(tag);
            if (!lastDigits.Success)
            {
                return tag + " _1";
            }

            int number = int.Parse(lastDigits.Groups[0].Value);

            number++;

            return lastDigitsRegex.Replace(tag, number.ToString());
        }

        public static void UpdateEdgegapAppTag(string tag)
        {
            // throw new NotImplementedException();
        }

        /// <summary>Run a Docker cmd with streaming log response. TODO: Plugin to other Docker cmds</summary>
        /// <returns>Throws if logs contain "ERROR"</returns>
        ///
        /// <param name="registryUrl">ex: "registry.edgegap.com"</param>
        /// <param name="repoUsername">ex: "robot$mycompany-asdf+client-push"</param>
        /// <param name="repoPasswordToken">Different from ApiToken; sometimes called "Container Registry Password"</param>
        /// <param name="onStatusUpdate">Log stream</param>
        // MIRROR CHANGE: CROSS PLATFORM SUPPORT
        static async Task<bool> RunCommand_DockerLogin(
            string registryUrl,
            string repoUsername,
            string repoPasswordToken,
            Action<string> outputReciever = null, Action<string> errorReciever = null)
        {
            // TODO: Use --password-stdin for security (!) This is no easy task for child Process | https://stackoverflow.com/q/51489359/6541639
            // (!) Don't use single quotes for cross-platform support (works unexpectedly in `cmd`).

            try
            {
#if UNITY_EDITOR_WIN
                await RunCommand("cmd.exe", $"/c docker login -u \"{repoUsername}\" --password \"{repoPasswordToken}\" \"{registryUrl}\"", outputReciever, errorReciever);
#elif UNITY_EDITOR_OSX
                await RunCommand("/bin/bash", $"-c \"docker login -u '{repoUsername}' --password '{repoPasswordToken}' '{registryUrl}'\"", outputReciever, errorReciever);
#elif UNITY_EDITOR_LINUX
                await RunCommand("/bin/bash", $"-c \"docker login -u '{repoUsername}' --password '{repoPasswordToken}' '{registryUrl}'\"", outputReciever, errorReciever);
#else
                Debug.LogError("The platform is not supported yet.");
#endif
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// v2: Login to Docker Registry via RunCommand(), returning streamed log messages:
        /// "docker login {registryUrl} {repository} {repoUsername} {repoPasswordToken}"
        /// </summary>
        /// <param name="registryUrl">ex: "registry.edgegap.com"</param>
        /// <param name="repoUsername">ex: "robot$mycompany-asdf+client-push"</param>
        /// <param name="repoPasswordToken">Different from ApiToken; sometimes called "Container Registry Password"</param>
        /// <param name="onStatusUpdate">Log stream</param>
        /// <returns>isSuccess</returns>
        public static async Task<bool> LoginContainerRegistry(
            string registryUrl,
            string repoUsername,
            string repoPasswordToken,
            Action<string> onStatusUpdate)
        {
            string error = null;
            await RunCommand_DockerLogin(registryUrl, repoUsername, repoPasswordToken, onStatusUpdate, msg => error = msg); // MIRROR CHANGE
            if (error.ToLowerInvariant().Contains("error") || error.ToLowerInvariant().Contains("invalid"))
            {
                throw new Exception(error);
            }
            return true;
        }

    }
}
#endif
