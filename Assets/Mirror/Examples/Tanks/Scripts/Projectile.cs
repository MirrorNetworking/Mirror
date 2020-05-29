using UnityEngine;

namespace Mirror.Examples.Tanks
{
    public class Projectile : NetworkBehaviour
    {
        public float destroyAfter = 1;
        public Rigidbody rigidBody;
        public float force = 1000;

        [Header("Game Stats")]
        public int damage;
        public GameObject source;

        public override void OnStartServer()
        {
            Invoke(nameof(DestroySelf), destroyAfter);
        }

        // set velocity for server and client. this way we don't have to sync the
        // position, because both the server and the client simulate it.
        void Start()
        {
            rigidBody.AddForce(transform.forward * force);
        }

        // destroy for everyone on the server
        [Server]
        void DestroySelf()
        {
            NetworkServer.Destroy(gameObject);
        }

        // ServerCallback because we don't want a warning if OnTriggerEnter is
        // called on the client
        [ServerCallback]
        void OnTriggerEnter(Collider co)
        {
            //Hit another player
            if (co.tag.Equals("Player") && co.gameObject != source)
            {
                //Apply damage
                co.GetComponent<Tank>().health -= damage;

                //update score on source
                source.GetComponent<Tank>().score += damage;
            }

            NetworkServer.Destroy(gameObject);
        }
    }
}
