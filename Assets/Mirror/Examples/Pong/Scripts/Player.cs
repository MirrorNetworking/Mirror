using UnityEngine;

namespace Mirror.Examples.Pong
{
    public class Player : NetworkBehaviour
    {
        public float speed = 30;

        // need to use FixedUpdate for rigidbody
        void FixedUpdate()
        {
            // only let the local player control the racket.
            // don't control other player's rackets
            if (!isLocalPlayer) return;

            float vertical = Input.GetAxisRaw("Vertical");
            GetComponent<Rigidbody2D>().velocity = new Vector2(0, vertical) * speed * Time.fixedDeltaTime;
        }
    }
}
