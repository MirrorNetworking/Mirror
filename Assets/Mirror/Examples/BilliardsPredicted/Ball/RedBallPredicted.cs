using UnityEngine;

namespace Mirror.Examples.BilliardsPredicted
{
    public class RedBallPredicted : NetworkBehaviour
    {
        // destroy when entering a pocket.
        // there's only one trigger in the scene (the pocket).
        [ServerCallback]
        void OnTriggerEnter(Collider other)
        {
            NetworkServer.Destroy(gameObject);
        }
    }
}
