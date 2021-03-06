using AssetStoreTools.Validator.Categories;
using AssetStoreTools.Validator.Data;
using UnityEditor;

namespace AssetStoreTools.Validator.TestDefinitions
{
    internal abstract class ValidationTest
    {
        public int Id;
        public string Title;
        public string Description;
        public MonoScript TestScript;

        public ValidatorCategory CategoryInfo;

        public TestResult Result;

        protected ValidationTest(ValidationTestScriptableObject source)
        {
            Id = source.Id;
            Title = source.Title;
            Description = source.Description;
            TestScript = source.TestScript;
            CategoryInfo = source.CategoryInfo;
            Result = new TestResult();
        }

        public abstract void Run(ValidationTestConfig config);
        
        public string Slugify(string value)
        {
            string newValue = value.Replace(' ', '-').ToLower();
            return newValue;
        }
    }
}
