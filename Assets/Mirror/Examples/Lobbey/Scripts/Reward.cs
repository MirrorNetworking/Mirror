using UnityEngine;

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
            GetComponent<Renderer>().material.color = color;
        }

        public bool available = true;
        public Spawner spawner;

        // This is called from PlayerController.CmdClaimPrize which is invoked by PlayerController.OnControllerColliderHit
        // This only runs on the server
        public void ClaimPrize(GameObject player)
        {
            if (available)
            {
                // This is a fast switch to prevent two players claiming the prize in a bang-bang close contest for it.
                // First hit turns it off, pending the object being destroyed a few frames later.
                available = false;

                // calculate the points from the color ... lighter scores higher as the average approaches 255
                // UnityEngine.Color RGB values are float fractions of 255
                uint points = (uint)(((prizeColor.r * 255) + (prizeColor.g * 255) + (prizeColor.b * 255)) / 3);
                if (LogFilter.Debug) Debug.LogFormat("Scored {0} points R:{1} G:{2} B:{3}", points, prizeColor.r, prizeColor.g, prizeColor.b);

                // award the points via SyncVar on the PlayerController
                player.GetComponent<PlayerController>().score += points;

                // spawn a replacement
                spawner.SpawnPrize();

                // destroy this one
                NetworkServer.Destroy(gameObject);
            }
        }
    }
}
