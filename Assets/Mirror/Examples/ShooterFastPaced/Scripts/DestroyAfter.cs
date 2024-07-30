using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class DestroyAfter : MonoBehaviour
    {
        public float time = 1;

        void Start()
        {
            Destroy(gameObject, time);
        }
    }
}
