using AssetStoreTools.Validator.TestDefinitions;
using System.Collections.Generic;

namespace AssetStoreTools.Validator.Data
{
    internal enum ValidationStatus
    {
        NotRun,
        RanToCompletion,
        Failed,
        Cancelled
    }

    internal class ValidationResult
    {
        public ValidationStatus Status;
        public List<AutomatedTest> AutomatedTests;
        public bool HadCompilationErrors;
        public string ProjectPath;
        public string Error;

        public ValidationResult()
        {
            Status = ValidationStatus.NotRun;
            AutomatedTests = new List<AutomatedTest>();
            HadCompilationErrors = false;
            ProjectPath = string.Empty;
            Error = string.Empty;
        }
    }
}