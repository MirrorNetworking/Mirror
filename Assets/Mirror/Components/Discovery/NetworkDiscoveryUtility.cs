using UnityEngine;

namespace Mirror
{
    public sealed class NetworkDiscoveryUtility
    {
        private NetworkDiscoveryUtility() { }

        public static bool RunSafe(System.Action action, bool logException = true)
        {
            try
            {
                action();
                return true;
            }
            catch (System.Exception ex)
            {
                if (logException)
                    Debug.LogException(ex);

                return false;
            }
        }
    }
}