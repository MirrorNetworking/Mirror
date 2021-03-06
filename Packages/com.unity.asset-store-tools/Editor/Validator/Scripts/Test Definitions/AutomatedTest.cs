using AssetStoreTools.Validator.Data;
using System;
using System.Linq;
using UnityEngine;

namespace AssetStoreTools.Validator.TestDefinitions
{
    internal class AutomatedTest : ValidationTest
    {
        public AutomatedTest(ValidationTestScriptableObject source) : base(source) { }

        public override void Run(ValidationTestConfig config)
        {
            Type testClass;
            if (TestScript == null || (testClass = TestScript.GetClass()) == null)
            {
                Debug.LogError($"Cannot run test {Title} - Test Script class was not found");
                return;
            }

            if (!testClass.GetInterfaces().Contains(typeof(ITestScript)))
            {
                Debug.LogError($"Cannot run test {Title} - Test Script class is not derived from {nameof(ITestScript)}");
                return;
            }

            var testMethod = testClass.GetMethod("Run");
            if (testMethod == null)
            {
                Debug.LogError($"Cannot run test {Title} - Run() method was not found");
                return;
            }

            try
            {
                Result = (TestResult)testMethod.Invoke(Activator.CreateInstance(testClass), new[] { config });
            }
            catch (Exception e)
            {
                var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };
                result.AddMessage("An exception was caught when running this test case. See Console for more details");
                Debug.LogError($"An exception was caught when running validation for test case '{Title}'\n{e.InnerException}");
                Result = result;
            }
        }
    }
}