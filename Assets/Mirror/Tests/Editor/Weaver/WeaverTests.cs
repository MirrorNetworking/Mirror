//#define LOG_WEAVER_OUTPUTS

using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public abstract class WeaverTestsBuildFromTestName : WeaverTests
    {
        [SetUp]
        public void TestSetup()
        {
            BuildAndWeaveTestAssembly(TestContext.CurrentContext.Test.Name);
        }
    }
    [TestFixture]
    [Category("Weaver")]
    public abstract class WeaverTests
    {
        protected List<string> weaverErrors = new List<string>();
        void HandleWeaverError(string msg)
        {
#if LOG_WEAVER_OUTPUTS
            Debug.LogError(msg);
#endif
            weaverErrors.Add(msg);
        }

        protected List<string> weaverWarnings = new List<string>();
        void HandleWeaverWarning(string msg)
        {
#if LOG_WEAVER_OUTPUTS
            Debug.LogWarning(msg);
#endif
            weaverWarnings.Add(msg);
        }

        protected void BuildAndWeaveTestAssembly(string baseName)
        {
            WeaverAssembler.OutputFile = baseName + ".dll";
            WeaverAssembler.AddSourceFiles(new string[] { baseName + ".cs" });
            WeaverAssembler.Build();

            Assert.That(WeaverAssembler.CompilerErrors, Is.False);
            if (weaverErrors.Count > 0)
            {
                Assert.That(weaverErrors[0], Does.StartWith("Mirror.Weaver error: "));
            }
        }

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            // TextRenderingModule is only referenced to use TextMesh type to throw errors about types from another module
            WeaverAssembler.AddReferencesByAssemblyName(new string[] { "UnityEngine.dll", "UnityEngine.CoreModule.dll", "UnityEngine.TextRenderingModule.dll", "Mirror.dll" });

            CompilationFinishedHook.UnityLogEnabled = false;
            CompilationFinishedHook.OnWeaverError += HandleWeaverError;
            CompilationFinishedHook.OnWeaverWarning += HandleWeaverWarning;
        }

        [OneTimeTearDown]
        public void FixtureCleanup()
        {
            CompilationFinishedHook.OnWeaverError -= HandleWeaverError;
            CompilationFinishedHook.OnWeaverWarning -= HandleWeaverWarning;
            CompilationFinishedHook.UnityLogEnabled = true;
        }

        [TearDown]
        public void TestCleanup()
        {
            WeaverAssembler.DeleteOutputOnClear = true;
            WeaverAssembler.Clear();

            weaverWarnings.Clear();
            weaverErrors.Clear();
        }
    }
}
