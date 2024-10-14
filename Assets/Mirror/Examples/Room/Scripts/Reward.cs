using UnityEngine;

namespace Mirror.Examples.NetworkRoom
{
    [AddComponentMenu("")]
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
            // Set up physics layers to prevent this from being called by non-players
            // and eliminate the need for a tag check here.
            if (!other.CompareTag("Player")) return;

            // This is a fast switch to prevent two players claiming the prize in a bang-bang close contest for it.
            // First to trigger turns it off, pending the object being destroyed a few frames later.
            if (!available)
                return;
            else
                available = false;

            // Calculate the points from the color...lighter scores higher as the average approaches 255
            // UnityEngine.Color RGB values are byte 0 to 255
            uint points = (uint)((randomColor.color.r + randomColor.color.g + randomColor.color.b) / 3);

            // award the points via SyncVar on Player's PlayerScore
            other.GetComponent<PlayerScore>().score += points;

            // spawn a replacement
            Spawner.SpawnReward();

            // destroy this one
            NetworkServer.Destroy(gameObject);
        }
    }
}
