using System;
using System.Collections.Generic;
using System.Linq;

namespace Mirror.SimpleWeb
{
    /// <summary>
    /// Represents a client's request to the Websockets server, which is the first message from the client.
    /// </summary>
    public class Request
    {
        private static readonly char[] lineSplitChars = new char[] { '\r', '\n' };
        public string RequestLine;
        public Dictionary<string, string> Headers = new Dictionary<string, string>();

        public Request(string message)
        {
            string[] all = message.Split(lineSplitChars, StringSplitOptions.RemoveEmptyEntries);
            // we need to add GET back in because ServerHandshake doesn't include it
            RequestLine = "GET" + all[0];
            Headers = all.Skip(1)
                         .Select(header => header.Split(':'))
                         .Where(split => split.Length == 2)
                         .ToDictionary(split => split[0].Trim(), split => split[1].Trim());
        }
    }
}
