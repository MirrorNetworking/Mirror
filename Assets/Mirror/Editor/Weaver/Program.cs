using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;

namespace Mirror.Weaver
{
    public static class Log
    {
        public static Action<string> WarningMethod;
        public static Action<string> ErrorMethod;

        public static void Warning(string msg)
        {
            WarningMethod("Mirror.Weaver warning: " + msg);
        }

        public static void Error(string msg)
        {
            ErrorMethod("Mirror.Weaver error: " + msg);
        }
    }

    public class Program
    {
        public static bool Process(string unityEngine, string netDLL, string outputDirectory, string[] assemblies, string[] extraAssemblyPaths, Action<string> printWarning, Action<string> printError)
        {
            CheckDLLPath(unityEngine);
            CheckDLLPath(netDLL);
            CheckOutputDirectory(outputDirectory);
            CheckAssemblies(assemblies);
            Log.WarningMethod = printWarning;
            Log.ErrorMethod = printError;
            return Weaver.WeaveAssemblies(assemblies, extraAssemblyPaths, null, outputDirectory, unityEngine, netDLL);
        }

        private static void CheckDLLPath(string path)
        {
            if (!File.Exists(path))
                throw new Exception("dll could not be located at " + path + "!");
        }

        private static void CheckAssemblies(IEnumerable<string> assemblyPaths)
        {
            foreach (var assemblyPath in assemblyPaths)
                CheckAssemblyPath(assemblyPath);
        }

        private static void CheckAssemblyPath(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
                throw new Exception("Assembly " + assemblyPath + " does not exist!");
        }

        private static void CheckOutputDirectory(string outputDir)
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);
        }
    }
}
