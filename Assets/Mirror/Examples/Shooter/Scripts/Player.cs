using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class Player : NetworkBehaviour
    {
        /*
        [Header("Camera")]
        public Transform cameraMount;
        Vector3 initialCameraPosition;
        Quaternion initialCameraRotation;
        Camera cam;

        protected virtual void Awake()
        {
            // find main camera once
            cam = Camera.main;
        }

        public override void OnStartLocalPlayer()
        {
            // remember initial camera position/rotation
            initialCameraPosition = cam.transform.position;
            initialCameraRotation = cam.transform.rotation;

            // move main camera into camera mount
            cam.transform.SetParent(cameraMount, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
        }

        public override void OnStopLocalPlayer()
        {
            // move the camera back to the original point.
            // otherwise it would be destroyed when stopping the game (and player)
            cam.transform.SetParent(null, true);
            cam.transform.position = initialCameraPosition;
            cam.transform.rotation = initialCameraRotation;
        }*/
    }
}
