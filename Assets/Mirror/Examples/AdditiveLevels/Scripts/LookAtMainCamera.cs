using UnityEngine;

namespace Mirror.Examples.AdditiveLevels
{
    // This script is attached to portal labels to keep them facing the camera
    public class LookAtMainCamera : MonoBehaviour
    {
        Transform mainCamTransform;

        private void Awake()
        {
            // flip scale so it's not bass-ackwards
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }

        void LateUpdate()
        {
            if (mainCamTransform == null)
                mainCamTransform = Camera.main.transform;

            if (mainCamTransform != null)
                transform.LookAt(mainCamTransform);
        }
    }
}
