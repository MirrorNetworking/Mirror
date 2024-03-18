using UnityEngine;

namespace Mirror.Examples.NetworkRoom
{
    [RequireComponent(typeof(Common.RandomColor))]
    public class Reward : NetworkBehaviour
    {
        [Header("Components")]
        public Common.RandomColor randomColor;

        [Header("Diagnostics")]
        [ReadOnly, SerializeField]
        bool available = true;

        protected override void OnValidate()
        {
            base.OnValidate();

            if (randomColor == null)
                randomColor = GetComponent<Common.RandomColor>();
        }

        [ServerCallback]
        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Player"))
                ClaimPrize(other.gameObject);
        }

        [ServerCallback]
        void ClaimPrize(GameObject player)
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
