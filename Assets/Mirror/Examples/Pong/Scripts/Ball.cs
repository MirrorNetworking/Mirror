using UnityEngine;

namespace Mirror.Examples.Pong
{
    public class Ball : NetworkBehaviour
    {
        public float speed = 30;

        Rigidbody2D rb;

        public override void OnStartServer()
        {
            base.OnStartServer();

            rb = GetComponent<Rigidbody2D>();

            // only simulate ball physics on server
            rb.simulated = true;

            rb.velocity = Vector2.right * speed;
        }

        //public void Start()
        //{
        //    // only simulate ball physics on server
        //    GetComponent<Rigidbody2D>().simulated = isServer;
        //    if (isServer)
        //        GetComponent<Rigidbody2D>().velocity = Vector2.right * speed;
        //}

        float HitFactor(Vector2 ballPos, Vector2 racquetPos, float racquetHeight)
        {
            // ascii art:
            // ||  1 <- at the top of the racquet
            // ||
            // ||  0 <- at the middle of the racquet
            // ||
            // || -1 <- at the bottom of the racquet
            return (ballPos.y - racquetPos.y) / racquetHeight;
        }

        [ServerCallback] // only call this on server
        void OnCollisionEnter2D(Collision2D col)
        {
            // Note: 'col' holds the collision information. If the
            // Ball collided with a racquet, then:
            //   col.gameObject is the racquet
            //   col.transform.position is the racquet's position
            //   col.collider is the racquet's collider

            // did we hit a racquet? then we need to calculate the hit factor
            if (col.transform.GetComponent<Player>())
            {
                // Calculate y direction via hit Factor
                float y = HitFactor(transform.position,
                                    col.transform.position,
                                    col.collider.bounds.size.y);

                // Calculate x direction via opposite collision
                float x = col.relativeVelocity.x > 0 ? 1 : -1;

                // Calculate direction, make length=1 via .normalized
                Vector2 dir = new Vector2(x, y).normalized;

                // Set Velocity with dir * speed
                rb.velocity = dir * speed;
            }
        }
    }
}
