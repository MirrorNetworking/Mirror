// parse session_id and user_id from command line args.
// mac: "open mirror.app --args session_id=123 user_id=456"
using System;
using UnityEngine;

namespace Edgegap
{
    public class RelayCredentialsFromArgs : MonoBehaviour
    {
        void Awake()
        {
            String cmd = Environment.CommandLine;

            // parse session_id via regex
            String sessionId = EdgegapKcpTransport.ReParse(cmd, "session_id=(\\d+)", "111111");
            String userID = EdgegapKcpTransport.ReParse(cmd, "user_id=(\\d+)", "222222");
            Debug.Log($"Parsed sessionId: {sessionId} user_id: {userID}");

            // configure transport
            EdgegapKcpTransport transport = GetComponent<EdgegapKcpTransport>();
            transport.sessionId = UInt32.Parse(sessionId);
            transport.userId = UInt32.Parse(userID);
        }
    }
}
