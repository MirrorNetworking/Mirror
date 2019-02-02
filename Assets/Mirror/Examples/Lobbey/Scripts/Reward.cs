using UnityEngine;
using Mirror;

namespace Mirror.Examples.NetworkLobby
{
    public class Reward : NetworkBehaviour
    {
        public override void OnStartServer()
        {
            base.OnStartServer();
            prizeColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
        }

        [SyncVar(hook = "SetColor")]
        public Color prizeColor = Color.black;

        void SetColor(Color color)
        {
            GetComponent<Renderer>().material.color = color;
        }

        bool available = true;

        public Spawner spawner;
        private void OnTriggerEnter(Collider other)
        {
            if (isServer && available)
            {
                // This is a fast switch to prevent two players claiming the prize in a bang-bang close contest for it.
                // First hit turns it off, pending the object being destroyed a few frames later.
                available = false;

                // calculate the points from the color ... darker scores higher
                uint points = (uint)(((prizeColor.r * 255) + (prizeColor.g * 255) + (prizeColor.b * 255)) / 3);
                Debug.LogFormat("Scored {0} points R:{1} G:{2} B:{3}", points, prizeColor.r, prizeColor.g, prizeColor.b);

                // award the points via SyncVar on the PlayerController
                other.GetComponent<PlayerController>().score += points;

                // spawn a replacement
                spawner.SpawnPrize();

                // destroy this one
                NetworkServer.Destroy(gameObject);
            }
        }
    }
}
