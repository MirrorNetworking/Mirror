using UnityEngine;
using System;
using NSubstitute;

namespace Mirror.Tests
{

    public static class LogExpect
    {
        public static void ExpectWarn(string warn, Action action)
        {
            ILogHandler defaultHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = Substitute.For<ILogHandler>();

            try
            {
                action();
                Debug.unityLogger.logHandler.Received().LogFormat(LogType.Warning, null, "{0}", warn);
            }
            finally
            {
                Debug.unityLogger.logHandler = defaultHandler;
            }
        }
    }
}