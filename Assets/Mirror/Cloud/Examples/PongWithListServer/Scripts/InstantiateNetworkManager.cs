using UnityEngine;

namespace Mirror.CloudServices.Examples
{
    public class InstantiateNetworkManager : MonoBehaviour
    {
        public GameObject prefab;

        private void Awake()
        {
            if (NetworkManager.singleton == null)
            {
                Instantiate(prefab);
            }
        }
    }
}
