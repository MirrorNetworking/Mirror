using UnityEngine;

namespace Mirror {
    [RequireComponent(typeof(NetworkIdentity))]
    class DistanceVisRangeOverride : MonoBehaviour
    {
        [Tooltip("The maximum range that objects will be visible at.")]
        public float visRange = 20;
    }
}
