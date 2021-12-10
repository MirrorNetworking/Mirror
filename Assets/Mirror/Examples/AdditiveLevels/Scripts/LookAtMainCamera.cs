using UnityEngine;

namespace Mirror.Examples.AdditiveLevels
{
    // This script is attached to portal labels to keep them facing the camera
    public class LookAtMainCamera : MonoBehaviour
    {
        // LateUpdate so that all camera updates are finished.
        void LateUpdate()
        {
            transform.forward = Camera.main.transform.forward;
        }
    }
}
