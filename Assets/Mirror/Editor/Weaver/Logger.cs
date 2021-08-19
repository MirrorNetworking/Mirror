using System;

namespace Mirror.Weaver
{
    // not static, because ILPostProcessor is multithreaded
    public class Logger
    {
        public Action<string> Warning;
        public Action<string> Error;

        public Logger(Action<string> Warning, Action<string>Error)
        {
            this.Warning = Warning;
            this.Error = Error;
        }
    }
}
