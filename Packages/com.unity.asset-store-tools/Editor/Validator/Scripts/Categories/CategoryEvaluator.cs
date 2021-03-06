using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;

namespace AssetStoreTools.Validator.Categories
{
    internal class CategoryEvaluator
    {
        private string _category;
        
        public CategoryEvaluator(string category)
        {
            _category = category;
        }

        public void SetCategory(string category)
        {
            _category = category;
        }

        public string GetCategory()
        {
            return _category;
        }
        
        public TestResult.ResultStatus Evaluate(ValidationTest validation, bool slugify = false)
        {
            var result = validation.Result.Result;
            if (result != TestResult.ResultStatus.VariableSeverityIssue)
                return result;

            var category = _category;
                
            if (slugify)
                category = validation.Slugify(category);

            return validation.CategoryInfo.EvaluateByFilter(category);
        }
        
        // Used by ab-builder
        public TestResult.ResultStatus EvaluateAndSlugify(ValidationTest validation)
        {
            return Evaluate(validation, true);
        }
    }
}