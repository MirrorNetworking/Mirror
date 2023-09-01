// Useful for Text Meshes that should face the camera.
using UnityEngine;

namespace Mirror.Examples.CharacterSelection
{
    public class FaceCamera : MonoBehaviour
    {
        private Transform cameraObj;

        // LateUpdate so that all camera updates are finished.
        void LateUpdate()
        {
            if (cameraObj == null || cameraObj.gameObject.activeInHierarchy == false)
            {
                cameraObj = Camera.main.transform;
            }

            if (cameraObj)
            {
                transform.forward = cameraObj.forward;
            }
        }
    }
}
