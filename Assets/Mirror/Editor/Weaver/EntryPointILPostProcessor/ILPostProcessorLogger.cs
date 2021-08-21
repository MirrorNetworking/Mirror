// logger for compilation finished hook.
// where we need a callback and Debug.Log.
using System.Collections.Generic;
using Mono.CecilX;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Mirror.Weaver
{
    public class ILPostProcessorLogger : Logger
    {
        // can't Debug.Log in ILPostProcessor. need to add to this list.
        internal List<DiagnosticMessage> Logs = new List<DiagnosticMessage>();

        public void LogDiagnostics(string message, DiagnosticType logType = DiagnosticType.Warning)
        {
            Logs.Add(new DiagnosticMessage
            {
                // TODO add file etc. for double click opening later?
                DiagnosticType = logType, // doesn't have .Log
                File = null,
                Line = 0,
                Column = 0,
                MessageData = $"Weaver: {message}"
            });
        }

        public void Warning(string message) => Warning(message, null);
        public void Warning(string message, MemberReference mr)
        {
            if (mr != null) message = $"{message} (at {mr})";
            LogDiagnostics(message, DiagnosticType.Warning);
        }

        public void Error(string message) => Error(message, null);
        public void Error(string message, MemberReference mr)
        {
            if (mr != null) message = $"{message} (at {mr})";
            LogDiagnostics(message, DiagnosticType.Error);
        }
    }
}
