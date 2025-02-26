using UnityEngine;
using UnityEngine.SceneManagement;

// This sets up the scene camera for the local player

namespace Mirror.Examples.Hex2D
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class Hex2DPlayerCamera : NetworkBehaviour
    {
        Camera mainCam;

        public Vector3 offset = new Vector3(0f, 40f, -65f);
        public Vector3 rotation = new Vector3(35f, 0f, 0f);

        [Header("Diagnostics")]
        [ReadOnly, SerializeField] HexSpatialHash2DInterestManagement.CheckMethod checkMethod;

        void Awake()
        {
            mainCam = Camera.main;
#if UNITY_2022_2_OR_NEWER
            checkMethod = FindAnyObjectByType<HexSpatialHash2DInterestManagement>().checkMethod;
#else
            checkMethod = FindObjectOfType<HexSpatialHash2DInterestManagement>().checkMethod;
#endif
        }

        public override void OnStartLocalPlayer()
        {
            if (mainCam != null)
            {
                // configure and make camera a child of player with 3rd person offset
                mainCam.transform.SetParent(transform);

                if (checkMethod == HexSpatialHash2DInterestManagement.CheckMethod.XY_FOR_2D)
                {
                    mainCam.orthographic = true;
                    mainCam.transform.localPosition = new Vector3(0, 0, -5f);
                    mainCam.transform.localEulerAngles = Vector3.zero;
                }
                else
                {
                    mainCam.orthographic = false;
                    mainCam.transform.localPosition = offset;
                    mainCam.transform.localEulerAngles = rotation;
                }
            }
            else
                Debug.LogWarning("PlayerCamera: Could not find a camera in scene with 'MainCamera' tag.");
        }

        void OnApplicationQuit()
        {
            //Debug.Log("PlayerCamera.OnApplicationQuit");
            ReleaseCamera();
        }

        public override void OnStopLocalPlayer()
        {
            //Debug.Log("PlayerCamera.OnStopLocalPlayer");
            ReleaseCamera();
        }

        void OnDisable()
        {
            //Debug.Log("PlayerCamera.OnDisable");
            ReleaseCamera();
        }

        void OnDestroy()
        {
            //Debug.Log("PlayerCamera.OnDestroy");
            ReleaseCamera();
        }

        void ReleaseCamera()
        {
            if (mainCam != null && mainCam.transform.parent == transform)
            {
                //Debug.Log("PlayerCamera.ReleaseCamera");

                mainCam.transform.SetParent(null);
                mainCam.orthographic = true;
                mainCam.orthographicSize = 15f;
                mainCam.transform.localPosition = new Vector3(0f, 70f, 0f);
                mainCam.transform.localEulerAngles = new Vector3(90f, 0f, 0f);

                if (mainCam.gameObject.scene != SceneManager.GetActiveScene())
                    SceneManager.MoveGameObjectToScene(mainCam.gameObject, SceneManager.GetActiveScene());
            }
        }
    }
}
