using UnityEngine;

namespace Mirror.Cloud.Examples.Pong
{
    public class Player : NetworkBehaviour
    {
        public float speed = 1500;
        public Rigidbody2D rigidbody2d;

        // need to use FixedUpdate for rigidbody
        void FixedUpdate()
        {
            // only let the local player control the racket.
            // don't control other player's rackets
            if (!isLocalPlayer)
                return;

            rigidbody2d.velocity = new Vector2(0, Input.GetAxisRaw("Vertical")) * speed * Time.fixedDeltaTime;
        }
    }
}
