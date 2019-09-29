using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// This component is used to make a gameObject a starting position for spawning player objects in multiplayer games.
    /// <para>This object's transform will be automatically registered and unregistered with the NetworkManager as a starting position.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkStartPosition")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkStartPosition.html")]
    public class NetworkStartPosition : MonoBehaviour
    {
        public void Awake()
        {
            NetworkManager.RegisterStartPosition(transform);
        }

        public void OnDestroy()
        {
            NetworkManager.UnRegisterStartPosition(transform);
        }
    }
}
