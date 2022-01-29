using UnityEngine;
using UnityEngine.SceneManagement;

// This sets up the scene camera for the local player

namespace Mirror.Examples.NetworkRoom
{
    public class PlayerCamera : NetworkBehaviour
    {
        Camera mainCam;

        void Awake()
        {
            mainCam = Camera.main;
        }

        public override void OnStartLocalPlayer()
        {
            if (mainCam != null)
            {
                // configure and make camera a child of player with 3rd person offset
                mainCam.orthographic = false;
                mainCam.transform.SetParent(transform);
                mainCam.transform.localPosition = new Vector3(0f, 3f, -8f);
                mainCam.transform.localEulerAngles = new Vector3(10f, 0f, 0f);
            }
        }

        public override void OnStopClient()
        {
            if (isLocalPlayer && mainCam != null)
            {
                mainCam.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(mainCam.gameObject, SceneManager.GetActiveScene());
                mainCam.orthographic = true;
                mainCam.transform.localPosition = new Vector3(0f, 70f, 0f);
                mainCam.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            }
        }
    }
}
