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

using Debug = UnityEngine.Debug;

namespace Edgegap
{
    internal static class EdgegapBuildUtils
    {
        public static bool IsArmCPU() =>
            RuntimeInformation.ProcessArchitecture == Architecture.Arm ||
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public static BuildReport BuildServer()
        {
            IEnumerable<string> scenes = EditorBuildSettings.scenes.Select(s=>s.path);
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
                locationPathName = "Builds/EdgegapServer/ServerBuild"
            };

            return BuildPipeline.BuildPlayer(options);
        }

        public static async Task<bool> DockerSetupAndInstallationCheck()
        {
            if (!File.Exists("Dockerfile"))
            {
                File.WriteAllText("Dockerfile", dockerFileText);
            }

            string output = null;
            string error = null;
            await RunCommand_DockerVersion(msg => output = msg, msg => error = msg); // MIRROR CHANGE
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error);
                return false;
            }
            Debug.Log($"[Edgegap] Docker version detected: {output}"); // MIRROR CHANGE
            return true;
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

        // MIRROR CHANGE
        public static async Task RunCommand_DockerBuild(string registry, string imageRepo, string tag, Action<string> onStatusUpdate)
        {
            string realErrorMessage = null;

            // ARM -> x86 support:
            // build commands use 'buildx' on ARM cpus for cross compilation.
            // otherwise docker builds would not launch when deployed because
            // Edgegap's infrastructure is on x86. instead the deployment logs
            // would show an error in a linux .go file with 'not found'.
            string buildCommand = IsArmCPU() ? "buildx build --platform linux/amd64" : "build";

#if UNITY_EDITOR_WIN
            await RunCommand("docker.exe", $"{buildCommand} -t {registry}/{imageRepo}:{tag} .", onStatusUpdate,
#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash", $"-c \"docker {buildCommand} -t {registry}/{imageRepo}:{tag} .\"", onStatusUpdate,
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", $"-c \"docker {buildCommand} -t {registry}/{imageRepo}:{tag} .\"", onStatusUpdate,
#endif
                (msg) =>
                {
                    if (msg.Contains("ERROR"))
                    {
                        realErrorMessage = msg;
                    }
                    onStatusUpdate(msg);
                });

            if(realErrorMessage != null)
            {
                throw new Exception(realErrorMessage);
            }
        }

        public static async Task<(bool, string)> RunCommand_DockerPush(string registry, string imageRepo, string tag, Action<string> onStatusUpdate)
        {
            string error = string.Empty;
#if UNITY_EDITOR_WIN
            await RunCommand("docker.exe", $"push {registry}/{imageRepo}:{tag}", onStatusUpdate, (msg) => error += msg + "\n");
#elif UNITY_EDITOR_OSX
            await RunCommand("/bin/bash", $"-c \"docker push {registry}/{imageRepo}:{tag}\"", onStatusUpdate, (msg) => error += msg + "\n");
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", $"-c \"docker push {registry}/{imageRepo}:{tag}\"", onStatusUpdate, (msg) => error += msg + "\n");
#endif
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error);
                return (false, error);
            }
            return (true, null);
        }
        // END MIRROR CHANGE

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

            // MIRROR CHANGE
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
            // END MIRROR CHANGE

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

        // -batchmode -nographics remains for Unity 2019/2020 support pre-dedicated server builds
        static string dockerFileText = @"FROM ubuntu:bionic

ARG DEBIAN_FRONTEND=noninteractive

COPY Builds/EdgegapServer /root/build/

WORKDIR /root/

RUN chmod +x /root/build/ServerBuild

ENTRYPOINT [ ""/root/build/ServerBuild"", ""-batchmode"", ""-nographics""]
";

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
            await RunCommand("/bin/bash", $"-c \"docker login -u \"{repoUsername}\" --password \"{repoPasswordToken}\" \"{registryUrl}\"\"", outputReciever, errorReciever);
#elif UNITY_EDITOR_LINUX
            await RunCommand("/bin/bash", $"-c \"docker login -u \"{repoUsername}\" --password \"{repoPasswordToken}\" \"{registryUrl}\"\"", outputReciever, errorReciever);
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
            if (error.Contains("ERROR"))
            {
                Debug.LogError(error);
                return false;
            }
            return true;
        }

    }
}
