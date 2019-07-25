using UnityEngine;

namespace Mirror.Examples.Pong
{
    public class Player : NetworkBehaviour
    {
        public float speed = 30;

        Rigidbody2D rb;

        public override void OnStartClient()
        {
            base.OnStartClient();
            rb = GetComponent<Rigidbody2D>();
        }

        // need to use FixedUpdate for rigidbody
        void FixedUpdate()
        {
            // only let the local player control the racquet.
            // don't control other player's racquets
            if (isLocalPlayer)
                rb.velocity = new Vector2(0, Input.GetAxisRaw("Vertical")) * speed * Time.fixedDeltaTime;
        }
    }
}
