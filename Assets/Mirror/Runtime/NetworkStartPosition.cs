using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkStartPosition")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkStartPosition")]
    public class NetworkStartPosition : MonoBehaviour
    {
        // NetworkManager.RebuildStartPositions() will collect all objects
        // in the scene with this component in depth-first hierarchy order

        public void OnDestroy()
        {
            NetworkManager.UnRegisterStartPosition(transform);
        }
    }
}
