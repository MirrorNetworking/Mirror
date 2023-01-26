using UnityEngine;
using UnityEngine.EventSystems;

namespace TestNT
{
    public class CameraMove : MonoBehaviour
    {
        enum CursorStates : byte { show, hide }
        enum RotationAxes : byte { MouseXAndY, MouseX, MouseY }

        [SerializeField, Range(0f, 1f)] float cameraPanSpeed = .2f;
        [SerializeField, Range(1f, 10f)] float cameraZoomSpeed = 5f;
        [SerializeField, Range(1f, 10f)] float cameraRotateSpeed = 3f;

        [SerializeField] RotationAxes rotationAxes = RotationAxes.MouseXAndY;

        void LateUpdate()
        {
            // this doesn't seem to work for allowing button clicks
            if (EventSystem.current.IsPointerOverGameObject())
                return;

            // workaround for above so can hold shift to click buttons
            if (!Input.GetKey(KeyCode.LeftShift))
                return;

            if (Input.GetMouseButton(0))
                CameraRotate();
            else if (Input.GetMouseButton(1))
                CameraOrbit();
            else if (Input.GetMouseButton(2))
                CameraPan();
            else
                ShowHideCursor(CursorStates.show);

            transform.position += transform.forward * cameraZoomSpeed * Input.GetAxis("Mouse ScrollWheel");
        }

        void CameraOrbit()
        {
            if (!transform.parent) return;

            ShowHideCursor(CursorStates.hide);
            transform.RotateAround(transform.parent.position, Vector3.up, Input.GetAxis("Mouse X") * cameraRotateSpeed);
        }

        void CameraRotate()
        {
            ShowHideCursor(CursorStates.hide);

            // Get initial value
            float rotationX = transform.localEulerAngles.x;

            if (rotationAxes == RotationAxes.MouseXAndY)
            {
                float rotationY = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * cameraRotateSpeed;
                rotationX -= Input.GetAxis("Mouse Y") * cameraRotateSpeed;
                transform.localEulerAngles = new Vector3(rotationX, rotationY, 0);
            }
            else if (rotationAxes == RotationAxes.MouseX)
            {
                transform.Rotate(0, Input.GetAxis("Mouse X") * cameraRotateSpeed, 0);
            }
            else
            {
                rotationX -= Input.GetAxis("Mouse Y") * cameraRotateSpeed;
                transform.localEulerAngles = new Vector3(rotationX, transform.localEulerAngles.y, 0);
            }
        }

        void CameraPan()
        {
            ShowHideCursor(CursorStates.hide);

            Vector3 mouseInputs = new Vector3(Input.GetAxis("Mouse X"), 0, Input.GetAxis("Mouse Y"));
            Vector3 pos = transform.position;

            if (mouseInputs.x > 0.0f)
                pos -= transform.right * cameraPanSpeed;

            if (mouseInputs.x < 0.0f)
                pos += transform.right * cameraPanSpeed;

            if (mouseInputs.z > 0.0f)
                pos += transform.forward * cameraPanSpeed;

            if (mouseInputs.z < 0.0f)
                pos -= transform.forward * cameraPanSpeed;

            pos.y = transform.position.y;

            transform.position = pos;
        }

        void ShowHideCursor(CursorStates cursorStates)
        {
            if (cursorStates == CursorStates.show)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (cursorStates == CursorStates.hide)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
