using System;
using System.Collections.Generic;
using System.Text;

namespace Ninja.WebSockets.Exceptions
{
    [Serializable]
    public class InvalidHttpResponseCodeException : Exception
    {
        public string ResponseCode { get; private set; }

        public string ResponseHeader { get; private set; }

        public string ResponseDetails { get; private set; }

        public InvalidHttpResponseCodeException() : base()
        {
        }

        public InvalidHttpResponseCodeException(string message) : base(message)
        {
        }

        public InvalidHttpResponseCodeException(string responseCode, string responseDetails, string responseHeader) : base(responseCode)
        {
            ResponseCode = responseCode;
            ResponseDetails = responseDetails;
            ResponseHeader = responseHeader;
        }

        public InvalidHttpResponseCodeException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
