// straight forward brute force interest management from DOTSNET
using UnityEngine;
namespace Mirror
{
    public class BruteForceInterestManagement : InterestManagement
    {
        // visibility radius
        public float visibilityRadius = float.MaxValue;

        // don't update every tick. update every so often.
        public float updateInterval = 1;
        double lastUpdateTime;

        public override void RebuildAll()
        {
            //RebuildObservers();
            //RemoveOldObservers();
            //AddNewObservers();
        }

        // update rebuilds every couple of seconds
        void Update()
        {
            // only while server is running
            if (NetworkServer.active)
            {
                if (Time.time >= lastUpdateTime + updateInterval)
                {
                    RebuildAll();
                    lastUpdateTime = Time.time;
                }
            }
        }
    }
}
