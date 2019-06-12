using UnityEngine;

namespace Mirror.Examples.ServerAuthoritativePlayerMovement
{
    public class DisableServerCamera : NetworkBehaviour
    {
        public bool DisableCamera = true; //only set this to false if you want to run in host mode

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (isServer && DisableCamera)
            {
                Camera MainCamera = FindObjectOfType<Camera>();
                MainCamera.gameObject.SetActive(false);
            }
        }
    }
}
