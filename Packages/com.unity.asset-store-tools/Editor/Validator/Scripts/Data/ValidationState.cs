using AssetStoreTools.Utility;
using AssetStoreTools.Utility.Json;
using AssetStoreTools.Validator.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AssetStoreTools.Validator.Data
{
    [Serializable]
    internal class ValidationStateData
    {
        public List<string> SerializedValidationPaths;
        public string SerializedCategory;
        public List<int> SerializedKeys;
        public List<TestResultData> SerializedValues;
        public bool HasCompilationErrors;
    }
        
    [Serializable]
    internal class TestResultData
    {
        public TestResult Result;
    }

    internal class ValidationState
    {
        public const string ValidationDataFilename = "AssetStoreValidationState.asset";
        public const string PersistentDataLocation = "Library";

        public Dictionary<int, TestResultData> TestResults = new Dictionary<int, TestResultData>();
        public ValidationStateData ValidationStateData;
        public Action OnJsonSave;

        private static ValidationState s_instance;
        public static ValidationState Instance
        {
            get
            {
                if (s_instance == null)
                    s_instance = new ValidationState();

                s_instance.LoadJson();

                return s_instance;
            }
        }

        private void LoadJson()
        {
            if (s_instance.TestResults.Count != 0)
                return;
            
            var saveFile = $"{PersistentDataLocation}/{ValidationDataFilename}";

            if (!File.Exists(saveFile))
            {
                s_instance.ValidationStateData = new ValidationStateData
                {
                    SerializedValidationPaths = new List<string>() { "Assets" },
                    SerializedCategory = ""
                };
                return;
            }
            
            var fileContents = File.ReadAllText(saveFile);
            var data = JsonUtility.FromJson<ValidationStateData>(fileContents);
            if (data.SerializedValidationPaths.Count == 0)
                data.SerializedValidationPaths.Add("Assets");
            s_instance.ValidationStateData = data;

            var implementedTests = ValidatorUtility.GetAutomatedTestCases();

            for (var i = 0; i < s_instance.ValidationStateData.SerializedKeys.Count; i++)
            {
                // Skip any potential tests that were serialized, but are no longer implemented
                if (!implementedTests.Any(x => x.Id == s_instance.ValidationStateData.SerializedKeys[i]))
                    continue;

                s_instance.TestResults.Add(s_instance.ValidationStateData.SerializedKeys[i], s_instance.ValidationStateData.SerializedValues[i]);
            }
        }

        public void SaveJson()
        {
            var saveFile = $"{PersistentDataLocation}/{ValidationDataFilename}";

            if (TestResults.Keys.Count == 0)
                return;

            ValidationStateData.SerializedKeys = TestResults.Keys.ToList();
            ValidationStateData.SerializedValues = TestResults.Values.ToList();
            
            var jsonString = JsonUtility.ToJson(ValidationStateData);

            File.WriteAllText(saveFile, jsonString);
            
            OnJsonSave?.Invoke();
        }

        public static bool GetValidationSummaryJson(ValidationStateData data, out string validationSummaryJson)
        {
            validationSummaryJson = string.Empty;
            try
            {
                var json = JsonValue.NewDict();

                // Construct compilation state
                json["has_compilation_errors"] = data.HasCompilationErrors;

                // Construct validation paths
                var pathsList = JsonValue.NewList();
                foreach (var path in data.SerializedValidationPaths)
                    pathsList.Add(path);
                json["validation_paths"] = pathsList;

                // Construct validation results
                var resultsDict = JsonValue.NewDict();
                for (int i = 0; i < data.SerializedKeys.Count; i++)
                {
                    var key = data.SerializedKeys[i].ToString();
                    var value = JsonValue.NewDict();
                    value["int"] = (int)data.SerializedValues[i].Result.Result;
                    value["string"] = data.SerializedValues[i].Result.Result.ToString();

                    resultsDict[key] = value;
                }
                json["validation_results"] = resultsDict;
                validationSummaryJson = json.ToString();
                return true;
            }
            catch (Exception e)
            {
                ASDebug.LogError($"Failed to parse a validation summary json:\n{e}");
                return false;
            }
        }

        public void CreateTestContainer(int testId)
        {
            s_instance.TestResults.Add(testId, new TestResultData());
        }

        public void ChangeResult(int index, TestResult result)
        {
            if (!s_instance.TestResults.ContainsKey(index))
                CreateTestContainer(index);
            
            s_instance.TestResults[index].Result = result;
        }

        public void SetCompilationState(bool hasCompilationErrors)
        {
            s_instance.ValidationStateData.HasCompilationErrors = hasCompilationErrors;
        }

        public void SetValidationPaths(string[] paths)
        {
            s_instance.ValidationStateData.SerializedValidationPaths = paths.ToList();
        }

        public void SetCategory(string category)
        {
            s_instance.ValidationStateData.SerializedCategory = category;
        }
        
    }
}