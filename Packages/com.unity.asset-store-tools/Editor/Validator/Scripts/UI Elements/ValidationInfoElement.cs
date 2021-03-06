using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Validator.UIElements
{
    internal class ValidationInfoElement : VisualElement
    {
        private const string GuidelinesUrl = "https://assetstore.unity.com/publishing/submission-guidelines#Overview";
        private const string SupportUrl = "https://support.unity.com/hc/en-us/requests/new?ticket_form_id=65905";
        
        public ValidationInfoElement()
        {
            ConstructInformationElement();
        }

        private void ConstructInformationElement()
        {
            AddToClassList("validation-info-box");
            
            var validatorDescription = new Label
            {
                text = "Validate your package to ensure your content follows the chosen submission guidelines. " +
                "The validations below do not cover all of the content standards, and passing all validations does not " +
                "guarantee that your package will be accepted to the Asset Store.\n\n" +
                "The tests are not obligatory for submitting your assets, but they can help avoid instant rejection by the " +
                "automated vetting system, or clarify reasons of rejection communicated by the vetting team.\n\n" +
                "For more information about the validations, view the message by expanding the tests or contact our support team."
            };
            validatorDescription.AddToClassList("validator-description");

            var submissionGuidelinesButton = new Button(() => OpenURL(GuidelinesUrl))
            {
                name = "GuidelinesButton",
                text = "Submission guidelines"
            };
            
            submissionGuidelinesButton.AddToClassList("hyperlink-button");
            
            var supportTicketButton = new Button(() => OpenURL(SupportUrl))
            {
                name = "SupportTicket",
                text = "Contact our support team"
            };
            
            supportTicketButton.AddToClassList("hyperlink-button");

            Add(validatorDescription);
            Add(submissionGuidelinesButton);
            Add(supportTicketButton);
        }

        private void OpenURL(string url)
        {
            Application.OpenURL(url);
        }
        
    }
}