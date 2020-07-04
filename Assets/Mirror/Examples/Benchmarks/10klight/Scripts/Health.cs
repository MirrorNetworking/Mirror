using System.Collections;
using UnityEngine;

namespace Mirror.Examples.Light
{
    public class Health : NetworkBehaviour
    {
        [SyncVar] public int health = 10;

        public void OnStartServer()
        {
            StartCoroutine(UpdateHealth());
        }

        public void OnStopServer()
        {
            StopAllCoroutines();
        }

        internal IEnumerator UpdateHealth()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(0f, 5f));
                health = (health + 1) % 10;
            }
        }
    }
}
