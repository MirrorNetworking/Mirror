using System;
using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class BatchedTestAttribute : TestAttribute
    {
        public BatchedTestAttribute(bool shouldCompileWithNoErrors)
        {
            ShouldCompileWithNoErrors = shouldCompileWithNoErrors;
        }

        public bool ShouldCompileWithNoErrors { get; set; }
    }
}
