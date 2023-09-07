using UnityEngine;
using Mirror;

namespace Mirror.Examples.CharacterSelection
{
    public class PlayerEmpty : NetworkBehaviour
    {
        private SceneReferencer sceneReferencer;

        public override void OnStartAuthority()
        {
            // enable UI located in the scene, after empty player spawns in.
            sceneReferencer = FindObjectOfType<SceneReferencer>();
            sceneReferencer.GetComponent<Canvas>().enabled = true;
        }
    }
}