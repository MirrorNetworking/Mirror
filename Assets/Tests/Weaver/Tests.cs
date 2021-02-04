using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NUnit.Framework;
using Unity.CompilationPipeline.Common.Diagnostics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Weaver
{
    public class AssertionMethodAttribute : Attribute { }

    public abstract class TestsBuildFromTestName : Tests
    {
        [SetUp]
        public virtual void TestSetup()
        {
#if LEGACY_ILPP
            LogAssert.ignoreFailingMessages = true;
#endif

            string className = TestContext.CurrentContext.Test.ClassName.Split('.').Last();

            BuildAndWeaveTestAssembly(className, TestContext.CurrentContext.Test.Name);
            
        }

        [TearDown]
        public virtual void TearDown()
        {
#if LEGACY_ILPP
            LogAssert.ignoreFailingMessages = false;
#endif
        }

        [AssertionMethod]
        protected void IsSuccess()
        {
            Assert.That(weaverLog.Diagnostics, Is.Empty);
        }

        [AssertionMethod]
        protected void HasError(string messsage, string atType)
        {
            Assert.That(weaverLog.Diagnostics
                .Where(d=> d.DiagnosticType == DiagnosticType.Error)
                .Select(d=> d.MessageData), Contains.Item($"{messsage} (at {atType})"));
        }

        [AssertionMethod]
        protected void HasWarning(string messsage, string atType)
        {
            Assert.That(weaverLog.Diagnostics
                .Where(d => d.DiagnosticType == DiagnosticType.Warning)
                .Select(d => d.MessageData), Contains.Item($"{messsage} (at {atType})"));
        }
    }

    [TestFixture]
    public abstract class Tests
    {
        public static readonly ILogger logger = LogFactory.GetLogger<Tests>(LogType.Exception);

        protected Logger weaverLog = new Logger();

        protected AssemblyDefinition assembly;

        protected void BuildAndWeaveTestAssembly(string className, string testName)
        {
            weaverLog.Diagnostics.Clear();

            string testSourceDirectory = className + "~";
            Assembler.OutputFile = Path.Combine(testSourceDirectory, testName + ".dll");
            Assembler.AddSourceFiles(new string[] { Path.Combine(testSourceDirectory, testName + ".cs") });
            assembly = Assembler.Build(weaverLog);
            Assert.That(Assembler.CompilerErrors, Is.False);

#if LEGACY_ILPP
            IEnumerable<DiagnosticMessage> diagnostics = ILPostProcessProgram.PostProcessResult.Diagnostics;
#else
            IEnumerable<DiagnosticMessage> diagnostics =weaverLog.Diagnostics;
#endif

            foreach (DiagnosticMessage error in weaverLog.Diagnostics)
            {
                // ensure all errors have a location
                Assert.That(error.MessageData, Does.Match(@"\(at .*\)$"));
            }

        }

        [TearDown]
        public void TestCleanup()
        {
            Assembler.DeleteOutputOnClear = true;
            Assembler.Clear();
        }
    }
}
