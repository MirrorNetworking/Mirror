using UnityEngine;
using UnityEngine.Rendering;

namespace Mirror
{
    public class HeadlessFrameLimiter : MonoBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger<HeadlessFrameLimiter>();

        /// <summary>
        /// Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.
        /// </summary>
        [Tooltip("Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.")]
        public int serverTickRate = 30;

        /// <summary>
        /// Set the frame rate for a headless server.
        /// </summary>
        public void Start()
        {
            // set a fixed tick rate instead of updating as often as possible
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Application.targetFrameRate = serverTickRate;
                if (logger.logEnabled) logger.Log("Server Tick Rate set to: " + Application.targetFrameRate + " Hz.");
            }
        }
    }
}
