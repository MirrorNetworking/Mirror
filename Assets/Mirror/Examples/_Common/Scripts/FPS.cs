using UnityEngine;

namespace Mirror.Examples.Common
{
    [AddComponentMenu("")]
    public class FPS : MonoBehaviour
    {
        // fps accessible to the outside
        public int framesPerSecond { get; private set; }

        // configuration
        public bool showGUI = true;
        public bool showLog = false;

        // helpers
        int count;
        double startTime;

        protected void Update()
        {
            ++count;
            if (Time.time >= startTime + 1)
            {
                framesPerSecond = count;
                startTime = Time.time;
                count = 0;
                if (showLog) Debug.Log($"FPS: {framesPerSecond}");
            }
        }

        protected void OnGUI()
        {
            if (!showGUI) return;

            GUI.Label(new Rect(Screen.width - 100, 0, 70, 25), $"FPS: {framesPerSecond}");
        }
    }
}