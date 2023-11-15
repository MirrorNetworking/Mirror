// Useful for Text Meshes that should face the camera.
using UnityEngine;

namespace Mirror.Examples.Tanks
{
    public class FaceCamera : MonoBehaviour
    {
        // LateUpdate so that all camera updates are finished.
        void LateUpdate()
        {
            transform.forward = Camera.main.transform.forward;
        }
    }
}
