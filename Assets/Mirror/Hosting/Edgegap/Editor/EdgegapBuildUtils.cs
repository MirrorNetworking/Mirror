using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;

using Debug = UnityEngine.Debug;

namespace Edgegap
{
    internal static class EdgegapBuildUtils
    {

        public static BuildReport BuildServer()
        {
            IEnumerable<string> scenes = EditorBuildSettings.scenes.Select(s=>s.path);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                target = BuildTarget.StandaloneLinux64,
 #pragma warning disable CS0618 // disable deprecated warning until Edgegap updates this
                options = BuildOptions.EnableHeadlessMode,
 #pragma warning restore CS0618
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

            string error = null;
            await RunCommand_DockerVersion(msg => error = msg); // MIRROR CHANGE
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error);
                return false;
            }
            return true;
        }

        // MIRROR CHANGE
        static async Task RunCommand_DockerVersion(Action<string> errorReciever = null)
        {
#if UNITY_EDITOR_WIN
            await RunCommand("cmd.exe", "/c docker --version", null, errorReciever);
#else
            Debug.LogError("The platform is not supported yet.");
#endif
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

            Process proc = new Process() { StartInfo = startInfo, };
            proc.EnableRaisingEvents = true;

            ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
            ConcurrentQueue<string> outputs = new ConcurrentQueue<string>();

            void PipeQueue(ConcurrentQueue<string> q, Action<string> opt)
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
                PipeQueue(errors, errorReciever);
                PipeQueue(outputs, outputReciever);
            }

            PipeQueue(errors, errorReciever);
            PipeQueue(outputs, outputReciever);
        }

        static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        public static async Task DockerBuild(string registry, string imageRepo, string tag, Action<string> onStatusUpdate)
        {
            string realErrorMessage = null;
            await RunCommand("docker.exe", $"build -t {registry}/{imageRepo}:{tag} .", onStatusUpdate,
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

        public static async Task<bool> DockerPush(string registry, string imageRepo, string tag, Action<string> onStatusUpdate)
        {
            string error = string.Empty;
            await RunCommand("docker.exe", $"push {registry}/{imageRepo}:{tag}", onStatusUpdate, (msg) => error += msg + "\n");
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error);
                return false;
            }
            return true;
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

        static string dockerFileText = @"FROM ubuntu:bionic

ARG DEBIAN_FRONTEND=noninteractive

COPY Builds/EdgegapServer /root/build/

WORKDIR /root/

RUN chmod +x /root/build/ServerBuild

ENTRYPOINT [ ""/root/build/ServerBuild"", ""-batchmode"", ""-nographics""]
";


    }
}
