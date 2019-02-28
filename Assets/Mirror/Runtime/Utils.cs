using UnityEngine;
using UnityEngine.Rendering;

namespace Mirror
{
    public static class Utils
    {
        // headless mode detection
        public static bool IsHeadless()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
        }
    }
}
