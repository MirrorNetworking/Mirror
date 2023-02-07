using UnityEngine;

namespace Mirror.Examples.AdditiveLevels
{
    // This script is attached to portal labels to keep them facing the camera
    public class LookAtMainCamera : MonoBehaviour
    {
        // This will be enabled by Portal script in OnStartClient
        void OnValidate()
        {
            this.enabled = false;
        }

        // LateUpdate so that all camera updates are finished.
        [ClientCallback]
        void LateUpdate()
        {
            transform.forward = Camera.main.transform.forward;
        }
    }
}
