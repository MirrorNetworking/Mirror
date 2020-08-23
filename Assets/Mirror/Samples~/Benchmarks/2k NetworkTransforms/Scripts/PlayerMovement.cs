using UnityEngine;

namespace Mirror.Examples.OneK
{
    public class PlayerMovement : NetworkBehaviour
    {
        public float speed = 5;

        void Update()
        {
            if (!IsLocalPlayer) return;

            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            var dir = new Vector3(h, 0, v);
            transform.position += dir.normalized * (Time.deltaTime * speed);
        }
    }
}
