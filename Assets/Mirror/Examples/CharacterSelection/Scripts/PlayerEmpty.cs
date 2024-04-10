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
#if UNITY_2022_2_OR_NEWER
            sceneReferencer = GameObject.FindAnyObjectByType<SceneReferencer>();
#else
            // Deprecated in Unity 2023.1
            sceneReferencer = GameObject.FindObjectOfType<SceneReferencer>();
#endif
            sceneReferencer.GetComponent<Canvas>().enabled = true;
        }
    }
}