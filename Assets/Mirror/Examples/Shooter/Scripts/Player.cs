using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class Player : NetworkBehaviour
    {
        public Transform cameraMount;

        public override void OnStartLocalPlayer()
        {
            // move main camera into camera mount
            Camera.main.transform.SetParent(cameraMount, false);
            Camera.main.transform.localPosition = Vector3.zero;
            Camera.main.transform.localRotation = Quaternion.identity;
        }
    }
}
