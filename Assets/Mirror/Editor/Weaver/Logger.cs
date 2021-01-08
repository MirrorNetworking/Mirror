using Mono.Cecil;
using UnityEngine;

namespace Mirror.Weaver
{
    public class Logger : IWeaverLogger
    {
        public void Error(string msg)
        {
            Debug.LogError(msg);
        }

        public void Error(string message, MemberReference mr)
        {
            Error($"{message} (at {mr})");
        }

        public void Warning(string message, MemberReference mr)
        {
            Warning($"{message} (at {mr})");
        }

        public void Warning(string msg)
        {
            Debug.LogWarning(msg);
        }
    }
}
