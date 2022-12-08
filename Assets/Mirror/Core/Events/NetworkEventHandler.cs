using System;

namespace Mirror.Core.Events
{
    [AttributeUsage(AttributeTargets.Method)]
    public class NetworkEventHandler : Attribute { }
}
