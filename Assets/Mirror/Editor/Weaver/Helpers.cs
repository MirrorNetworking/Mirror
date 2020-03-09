using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Mono.CecilX;

namespace Mirror.Weaver
{
    class Helpers
    {
        // This code is taken from SerializationWeaver

        public static string UnityEngineDLLDirectoryName()
        {
            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            return directoryName?.Replace(@"file:\", "");
        }

        public static string DestinationFileFor(string outputDir, string assemblyPath)
        {
            string fileName = Path.GetFileName(assemblyPath);
            Debug.Assert(fileName != null, "fileName != null");

            return Path.Combine(outputDir, fileName);
        }
    }
}
