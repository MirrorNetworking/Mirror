using System;
using System.Net;
using System.Net.Http;

namespace AssetStoreTools.Utility
{
    /// <summary>
    /// A structure for retrieving and converting errors from Asset Store Tools class methods
    /// </summary>
    internal class ASError
    {
        public string Message { get; private set; }
        public Exception Exception { get; private set; }

        public ASError() { }

        public static ASError GetGenericError(Exception ex)
        {
            ASError error = new ASError()
            {
                Message = ex.Message,
                Exception = ex
            };

            return error;
        }

        public static ASError GetLoginError(HttpResponseMessage response) => GetLoginError(response, null);

        public static ASError GetLoginError(HttpResponseMessage response, HttpRequestException ex)
        {
            ASError error = new ASError() { Exception = ex };

            switch (response.StatusCode)
            {
                // Add common error codes here
                case HttpStatusCode.Unauthorized:
                    error.Message = "Incorrect email and/or password. Please try again.";
                    break;
                case HttpStatusCode.InternalServerError:
                    error.Message = "Authentication request failed\nIf you were logging in with your Unity Cloud account, please make sure you are still logged in.\n" +
                        "This might also be caused by too many invalid login attempts - if that is the case, please try again later.";
                    break;
                default:
                    ParseHtmlMessage(response, out string message);
                    error.Message = message;
                    break;
            }

            return error;
        }

        public static ASError GetPublisherNullError(string publisherName)
        {
            ASError error = new ASError
            {
                Message = $"Your Unity ID {publisherName} is not currently connected to a publisher account. " +
                          $"Please create a publisher profile."
            };

            return error;
        }

        private static bool ParseHtmlMessage(HttpResponseMessage response, out string message)
        {
            message = "An undefined error has been encountered";
            string html = response.Content.ReadAsStringAsync().Result;

            if (!html.Contains("<!DOCTYPE HTML")) 
                return false;
            
            message += " with the following message:\n\n";
            var startIndex = html.IndexOf("<p>", StringComparison.Ordinal) + "<p>".Length;
            var endIndex = html.IndexOf("</p>", StringComparison.Ordinal);
            
            if (startIndex == -1 || endIndex == -1)
                return false;

            string htmlBodyMessage = html.Substring(startIndex, (endIndex - startIndex));
            htmlBodyMessage = htmlBodyMessage.Replace("\n", " ");

            message += htmlBodyMessage;
            message += "\n\nIf this error message is not very informative, please report this to Unity";

            return true;
        }

        public override string ToString()
        {
            return Message;
        }
    }
}