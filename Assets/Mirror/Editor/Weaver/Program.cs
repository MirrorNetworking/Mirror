using System;
using System.Collections.Generic;
using System.IO;

namespace Mirror.Weaver
{
    public static class Log
    {
        public static Action<string> WarningMethod;
        public static Action<string> ErrorMethod;

        public static void Warning(string msg)
        {
            WarningMethod(msg);
        }

        public static void Error(string msg)
        {
            ErrorMethod(msg);
        }
    }

    public static class Program
    {
        public static bool Process(string unityEngine, string netDLL, string outputDirectory, string[] assemblies, string[] extraAssemblyPaths, Action<string> printWarning, Action<string> printError)
        {
            CheckDllPath(unityEngine);
            CheckDllPath(netDLL);
            CheckOutputDirectory(outputDirectory);
            CheckAssemblies(assemblies);
            Log.WarningMethod = printWarning;
            Log.ErrorMethod = printError;
            return Weaver.WeaveAssemblies(assemblies, extraAssemblyPaths, outputDirectory, unityEngine, netDLL);
        }

        static void CheckDllPath(string path)
        {
            if (!File.Exists(path))
                throw new Exception("dll could not be located at " + path + "!");
        }

        static void CheckAssemblies(IEnumerable<string> assemblyPaths)
        {
            foreach (string assemblyPath in assemblyPaths)
                CheckAssemblyPath(assemblyPath);
        }

        static void CheckAssemblyPath(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
                throw new Exception("Assembly " + assemblyPath + " does not exist!");
        }

        static void CheckOutputDirectory(string outputDir)
        {
            if (outputDir != null && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
        }
    }
}
