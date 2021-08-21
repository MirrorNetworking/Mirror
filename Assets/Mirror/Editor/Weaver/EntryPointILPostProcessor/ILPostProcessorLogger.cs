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
            // DiagnosticMessage can't display \n for some reason.
            // it just cuts it off and we don't see any stack trace.
            // so let's replace all line breaks so we get the stack trace.
            // (Unity 2021.2.0b6 apple silicon)
            message = message.Replace("\n", "/");

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
