using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Weaver.Tests
{
    /*
        Test flow:

        * Find all tests with BatchedTestAttribute

        * Run tests in 2 groups
  
          * Successful compile tests
          * Error checking tests

        * If dll fails to compile
  
          * then run the tests one at at a time to find out which one fails

        * else (dll compiled)
  
          * mark tests as passed
          * check weaver gave errors

    */

    public class WeaverTestResults
    {
        public bool compileError;
        public bool weaverError;
        public List<string> weaverErrors = new List<string>();
        public List<string> weaverWarnings = new List<string>();

        public void CopyFrom(WeaverTestResults source)
        {
            compileError = source.compileError;
            weaverError = source.weaverError;

            weaverErrors.Clear();
            weaverErrors.AddRange(source.weaverErrors);

            weaverWarnings.Clear();
            weaverWarnings.AddRange(source.weaverWarnings);
        }

        public void Clear()
        {
            compileError = false;
            weaverError = false;
            weaverErrors.Clear();
            weaverWarnings.Clear();
        }

        public bool HasErrors()
        {
            return compileError
                || weaverError
                || weaverErrors.Count > 0;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class BatchedTestAttribute : TestAttribute
    {
        public BatchedTestAttribute(bool shouldCompileWithNoErrors)
        {
            ShouldCompileWithNoErrors = shouldCompileWithNoErrors;
        }

        public bool ShouldCompileWithNoErrors { get; set; }
    }

    /// <summary>
    /// Base class to used to run tests in baches
    /// </summary>
    [TestFixture]
    [Category("Weaver")]
    public abstract class BatchedWeaverTests
    {
        public static readonly UnityEngine.ILogger logger = LogFactory.GetLogger<BatchedWeaverTests>(LogType.Exception);

        // Results for tests that should compile with no errors
        WeaverTestResults successTestsResults = new WeaverTestResults();
        // Results for tests that should compile with weaver errors
        WeaverTestResults errorTestsResults = new WeaverTestResults();

        // Results for currently running test
        WeaverTestResults currentTestResults = new WeaverTestResults();

        string className => TestContext.CurrentContext.Test.ClassName.Split('.').Last();
        string CurrentTest => TestContext.CurrentContext.Test.Name;


        public void AssertThatWeaverErrorIs(bool value) => Assert.That(currentTestResults.weaverError, Is.EqualTo(value));
        public void AssertErrorListHasMatch(string value)
        {
            Assert.That(currentTestResults.weaverErrors,
                Has.Some.Match(value),
                "Expected: \n" +
                value + "\n" +
                "Error Messages: \n" +
                string.Join("\n", currentTestResults.weaverErrors));
        }

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            // TextRenderingModule is only referenced to use TextMesh type to throw errors about types from another module
            WeaverAssembler.AddReferencesByAssemblyName(new string[] { "UnityEngine.dll", "UnityEngine.CoreModule.dll", "UnityEngine.TextRenderingModule.dll", "Mirror.dll" });

            CompilationFinishedHook.UnityLogEnabled = false;
            CompilationFinishedHook.OnWeaverError += HandleWeaverError;
            CompilationFinishedHook.OnWeaverWarning += HandleWeaverWarning;

            (MethodInfo method, BatchedTestAttribute attribute)[] allBatchTest = GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(x => (method: x, attribute: x.GetCustomAttribute<BatchedTestAttribute>()))
                .Where(x => x.attribute != null)
                .ToArray();


            IEnumerable<string> successTests = allBatchTest
                .Where(x => x.attribute.ShouldCompileWithNoErrors)
                .Select(x => x.method.Name);
            IEnumerable<string> errorTests = allBatchTest
                .Where(x => !x.attribute.ShouldCompileWithNoErrors)
                .Select(x => x.method.Name);

            RunBuildAndWeave(successTests);
            successTestsResults.CopyFrom(currentTestResults);

            RunBuildAndWeave(errorTests);
            errorTestsResults.CopyFrom(currentTestResults);
        }

        [SetUp]
        public void TestSetup()
        {
            MethodInfo method = GetType().GetMethod(CurrentTest, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            BatchedTestAttribute attribute = method.GetCustomAttribute<BatchedTestAttribute>();

            // not batch test, build by itself
            if (attribute == null)
            {
                RunBuildAndWeave(CurrentTest);
            }
            else if (attribute.ShouldCompileWithNoErrors)
            {
                // success tests have errors, run each by itself
                if (successTestsResults.HasErrors())
                {
                    RunBuildAndWeave(CurrentTest);
                }
                else
                {
                    currentTestResults.CopyFrom(successTestsResults);
                }
            }
            else
            {
                // errors tests dont compile, run each by itself
                if (errorTestsResults.compileError)
                {
                    RunBuildAndWeave(CurrentTest);
                }
                else
                {
                    currentTestResults.CopyFrom(errorTestsResults);
                }

                // should have weaver error
                Assert.That(currentTestResults.weaverError, Is.True);
            }


            if (currentTestResults.compileError)
            {
                Assert.Fail($"Could not compile code for {CurrentTest}");
            }
        }

        [TearDown]
        public void TestCleanup()
        {
            WeaverAssembler.DeleteOutputOnClear = true;
            WeaverAssembler.Clear();

            currentTestResults.Clear();
        }

        [OneTimeTearDown]
        public void FixtureCleanup()
        {
            WeaverAssembler.ClearReferences();

            CompilationFinishedHook.OnWeaverError -= HandleWeaverError;
            CompilationFinishedHook.OnWeaverWarning -= HandleWeaverWarning;
            CompilationFinishedHook.UnityLogEnabled = true;

            successTestsResults.Clear();
            errorTestsResults.Clear();
            currentTestResults.Clear();
        }


        void RunBuildAndWeave(string testNames)
        {
            RunBuildAndWeave(new string[] { testNames });
        }
        void RunBuildAndWeave(IEnumerable<string> tests)
        {
            // pre build cleanup
            WeaverAssembler.DeleteOutputOnClear = true;
            WeaverAssembler.Clear();

            currentTestResults.Clear();

            // build
            BuildAndWeaveTestAssembly(className, tests);

            // set results
            currentTestResults.compileError = WeaverAssembler.CompilerErrors;
            currentTestResults.weaverError = CompilationFinishedHook.WeaveFailed;

            // post build cleanup
            WeaverAssembler.DeleteOutput();
        }

        void BuildAndWeaveTestAssembly(string className, IEnumerable<string> testNames)
        {
            string testSourceDirectory = className + "~";
            WeaverAssembler.OutputFile = Path.Combine(testSourceDirectory, className + ".dll");

            string[] sourceFiles = testNames.Select(testName => Path.Combine(testSourceDirectory, testName + ".cs")).ToArray();
            if (sourceFiles.Length == 0)
                return;

            if (sourceFiles.Length == 1)
            {
                WeaverAssembler.OutputFile = Path.ChangeExtension(sourceFiles[0], ".dll");
            }


            WeaverAssembler.AddSourceFiles(sourceFiles);
            WeaverAssembler.Build();
        }

        void HandleWeaverError(string msg)
        {
            logger.LogError(msg);
            currentTestResults.weaverErrors.Add(msg);
        }

        void HandleWeaverWarning(string msg)
        {
            logger.LogWarning(msg);
            currentTestResults.weaverWarnings.Add(msg);
        }
    }
}
