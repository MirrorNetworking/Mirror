using UnityEngine;
using Mirror;

namespace Mirror.Examples.NetworkRoom
{
    public class PlayerScore : NetworkBehaviour
    {
        [SyncVar]
        public int index;

        [SyncVar]
        public uint score;

        GameObject controllerColliderHitObject;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // If player and prize objects are on their own layer(s) with correct
            // collision matrix, we wouldn't have to validate the hit.gameobject.
            // Since this is just an example, project settings aren't included so we check the name.

            controllerColliderHitObject = hit.gameObject;

            if (isLocalPlayer && controllerColliderHitObject.name.StartsWith("Prize"))
            {
                if (LogFilter.Debug) Debug.LogFormat("OnControllerColliderHit {0}[{1}] with {2}[{3}]", name, netId, controllerColliderHitObject.name, controllerColliderHitObject.GetComponent<NetworkIdentity>().netId);

                // Disable the prize gameobject so it doesn't impede player movement
                // It's going to be destroyed in a few frames and we don't want to spam CmdClaimPrize.
                // OnControllerColliderHit will fire many times as the player slides against the object.
                controllerColliderHitObject.SetActive(false);

                CmdClaimPrize(controllerColliderHitObject);
            }
        }

        [Command]
        void CmdClaimPrize(GameObject hitObject)
        {
            // Null check is required, otherwise close timing of multiple claims could throw a null ref.
            if (hitObject != null)
            {
                hitObject.GetComponent<Reward>().ClaimPrize(gameObject);
            }
        }

        void OnGUI()
        {
            GUI.Box(new Rect(10f + (index * 110), 10f, 100f, 25f), score.ToString().PadLeft(10));
        }
    }
}
