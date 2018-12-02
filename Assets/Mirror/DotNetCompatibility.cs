using System;

namespace Mirror
{
    internal static class DotNetCompatibility
    {
        internal static string GetMethodName(this Delegate func)
        {
#if NETFX_CORE
            return func.GetMethodInfo().Name;
#else
            return func.Method.Name;
#endif
        }
    }
}
