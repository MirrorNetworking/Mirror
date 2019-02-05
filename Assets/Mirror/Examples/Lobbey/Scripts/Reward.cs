using UnityEngine;
using Mirror;

namespace Mirror.Examples.NetworkLobby
{
    public class Reward : NetworkBehaviour
    {
        [SyncVar(hook = "SetColor")]
        public Color prizeColor = Color.black;

        private void Start()
        {
            // This is a workaround pending a fix for https://github.com/vis2k/Mirror/issues/372
            SetColor(prizeColor);
        }

        void SetColor(Color color)
        {
            //Debug.LogWarningFormat("Reward SetColor netId:{0} to {1}", netId, color);
            GetComponent<Renderer>().material.color = color;
        }

        bool available = true;
        public Spawner spawner;
        private void OnTriggerEnter(Collider other)
        {
            // Set Players and Prizes to their own layer(s) and set up collision matrix
            // Doing so means we don't have to validate the 'other' collider.

            if (isServer && available)
            {
                // This is a fast switch to prevent two players claiming the prize in a bang-bang close contest for it.
                // First hit turns it off, pending the object being moved or destroyed a few frames later.
                available = false;

                // calculate the points from the color ... lighter scores higher as the average approaches 255
                uint points = (uint)(((prizeColor.r * 255) + (prizeColor.g * 255) + (prizeColor.b * 255)) / 3);
                Debug.LogFormat("Scored {0} points R:{1} G:{2} B:{3}", points, prizeColor.r, prizeColor.g, prizeColor.b);

                // award the points via SyncVar on the PlayerController
                other.GetComponent<PlayerController>().score += points;

                // destroy this one
                NetworkServer.Destroy(gameObject);

                //... and spawn a replacement
                spawner.SpawnPrize();
            }
        }
    }
}
