namespace Edgegap
{
    public enum ApiEnvironment
    {
        Staging,
        Console,
    }

    public static class ApiEnvironmentsExtensions
    {
        public static string GetApiUrl(this ApiEnvironment apiEnvironment)
        {
            string apiUrl;

            switch (apiEnvironment)
            {
                case ApiEnvironment.Staging:
                    apiUrl = "https://staging-api.edgegap.com";
                    break;
                case ApiEnvironment.Console:
                    apiUrl = "https://api.edgegap.com";
                    break;
                default:
                    apiUrl = null;
                    break;
            }

            return apiUrl;
        }

        public static string GetDashboardUrl(this ApiEnvironment apiEnvironment)
        {
            string apiUrl;

            switch (apiEnvironment)
            {
                case ApiEnvironment.Staging:
                    apiUrl = "https://staging-console.edgegap.com";
                    break;
                case ApiEnvironment.Console:
                    apiUrl = "https://console.edgegap.com";
                    break;
                default:
                    apiUrl = null;
                    break;
            }

            return apiUrl;
        }

        public static string GetDocumentationUrl(this ApiEnvironment apiEnvironment)
        {
            string apiUrl;

            switch (apiEnvironment)
            {
                case ApiEnvironment.Staging:
                    apiUrl = "https://staging-docs.edgegap.com/docs";
                    break;
                case ApiEnvironment.Console:
                    apiUrl = "https://docs.edgegap.com/docs";
                    break;
                default:
                    apiUrl = null;
                    break;
            }

            return apiUrl;
        }
    }
}