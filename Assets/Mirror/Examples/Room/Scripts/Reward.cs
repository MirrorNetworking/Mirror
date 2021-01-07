using UnityEngine;

namespace Mirror.Examples.NetworkRoom
{
    [RequireComponent(typeof(RandomColor))]
    public class Reward : NetworkBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(Reward));

        public bool available = true;
        public RandomColor randomColor;

        void OnValidate()
        {
            if (randomColor == null)
                randomColor = GetComponent<RandomColor>();
        }

        [ServerCallback]
        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                ClaimPrize(other.gameObject);
            }
        }

        // This is called from PlayerController.CmdClaimPrize which is invoked by PlayerController.OnControllerColliderHit
        // This only runs on the server
        public void ClaimPrize(GameObject player)
        {
            if (available)
            {
                // This is a fast switch to prevent two players claiming the prize in a bang-bang close contest for it.
                // First hit turns it off, pending the object being destroyed a few frames later.
                available = false;

                Color32 color = randomColor.color;

                // calculate the points from the color ... lighter scores higher as the average approaches 255
                // UnityEngine.Color RGB values are float fractions of 255
                uint points = (uint)(((color.r) + (color.g) + (color.b)) / 3);
                if (logger.LogEnabled()) logger.LogFormat(LogType.Log, "Scored {0} points R:{1} G:{2} B:{3}", points, color.r, color.g, color.b);

                // award the points via SyncVar on the PlayerController
                player.GetComponent<PlayerScore>().score += points;

                // spawn a replacement
                Spawner.SpawnReward();

                // destroy this one
                NetworkServer.Destroy(gameObject);
            }
        }
    }
}
