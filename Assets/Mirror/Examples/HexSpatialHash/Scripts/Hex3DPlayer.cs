using UnityEngine;
using Mirror;

namespace Mirror.Examples.Hex3D
{
    [AddComponentMenu("")]
    public class Hex3DPlayer : NetworkBehaviour
    {
        [Range(1, 20)]
        public float speed = 10;

        void Update()
        {
            if (!isLocalPlayer) return;

            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            // if left shift is held, apply v to y instead of z
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Vector3 dir = new Vector3(h, v, 0);
                transform.position += dir.normalized * (Time.deltaTime * speed);
            }
            else
            {
                Vector3 dir = new Vector3(h, 0, v);
                transform.position += dir.normalized * (Time.deltaTime * speed);
            }

            if (Input.GetKey(KeyCode.Q))
                transform.Rotate(Vector3.up, -90 * Time.deltaTime);
            if (Input.GetKey(KeyCode.E))
                transform.Rotate(Vector3.up, 90 * Time.deltaTime);
        }

#if !UNITY_SERVER
        void OnGUI()
        {
            if (isLocalPlayer)
            {
                GUILayout.BeginArea(new Rect(10, Screen.height - 50, 300, 300));
                GUILayout.Label("Use WASD+QE to move and rotate\nHold Shift with W/S to move up/down");
                GUILayout.EndArea();
            }
        }
#endif
    }
}
