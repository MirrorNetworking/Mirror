using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Weaver.Tests
{
    [TestFixture]
    [Category("Weaver")]
    public abstract class BatchedWeaverTests 
    {
        public static readonly ILogger logger = LogFactory.GetLogger<WeaverTests>(LogType.Exception);

        protected List<string> weaverErrors = new List<string>();
        void HandleWeaverError(string msg)
        {
            logger.LogError(msg);
            weaverErrors.Add(msg);
        }

        protected List<string> weaverWarnings = new List<string>();
        void HandleWeaverWarning(string msg)
        {
            logger.LogWarning(msg);
            weaverWarnings.Add(msg);
        }

        [OneTimeSetUp]
        public void TestSetup()
        {
            CompilationFinishedHook.UnityLogEnabled = false;
            CompilationFinishedHook.OnWeaverError += HandleWeaverError;
            CompilationFinishedHook.OnWeaverWarning += HandleWeaverWarning;

            string className = TestContext.CurrentContext.Test.ClassName.Split('.').Last();

            BuildAndWeaveTestAssembly(className);
        }

        protected void BuildAndWeaveTestAssembly(string className)
        {
            // TextRenderingModule is only referenced to use TextMesh type to throw errors about types from another module
            WeaverAssembler.AddReferencesByAssemblyName(new string[] { "UnityEngine.dll", "UnityEngine.CoreModule.dll", "UnityEngine.TextRenderingModule.dll", "Mirror.dll" });

            string testSourceDirectory = className + "~";
            WeaverAssembler.OutputFile = Path.Combine(testSourceDirectory, className + ".dll");

            // find all the cs files in that folder
            string[] sources =
                Directory.EnumerateFiles(Path.Combine(WeaverAssembler.OutputDirectory, testSourceDirectory), "*.cs")
                .ToArray();

            
            WeaverAssembler.AddSourceFilesFullPath(sources);
            WeaverAssembler.Build();
        }

        [OneTimeTearDown]
        public void FixtureCleanup()
        {
            CompilationFinishedHook.OnWeaverError -= HandleWeaverError;
            CompilationFinishedHook.OnWeaverWarning -= HandleWeaverWarning;
            CompilationFinishedHook.UnityLogEnabled = true;
       
            WeaverAssembler.DeleteOutputOnClear = true;
            WeaverAssembler.Clear();

            weaverWarnings.Clear();
            weaverErrors.Clear();
        }
    }
}