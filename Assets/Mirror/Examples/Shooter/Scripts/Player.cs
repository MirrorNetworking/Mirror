using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class Player : NetworkBehaviour
    {
        [Header("Camera")]
        public Transform cameraMount;
        Vector3 initialCameraPosition;
        Quaternion initialCameraRotation;

        public override void OnStartLocalPlayer()
        {
            // remember initial camera position/rotation
            initialCameraPosition = Camera.main.transform.position;
            initialCameraRotation = Camera.main.transform.rotation;

            // move main camera into camera mount
            Camera.main.transform.SetParent(cameraMount, false);
            Camera.main.transform.localPosition = Vector3.zero;
            Camera.main.transform.localRotation = Quaternion.identity;
        }

        public override void OnStopLocalPlayer()
        {
            // move the camera back to the original point
            Camera.main.transform.SetParent(null, true);
            Camera.main.transform.position = initialCameraPosition;
            Camera.main.transform.rotation = initialCameraRotation;
        }
    }
}
