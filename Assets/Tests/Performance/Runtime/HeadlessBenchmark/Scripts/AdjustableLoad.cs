using System.Collections;
using UnityEngine;

namespace Mirror.HeadlessBenchmark
{
    public class AdjustableLoad : NetworkBehaviour
    {
        public float MovementSpeed = 1;
        public float RotateSpeed = 50;

        [SyncVar]
        public float Floaty;

        public void Start()
        {
            NetIdentity.OnStartAuthority.AddListener(() => StartCoroutine(Move()));
        }

        private IEnumerator Move()
        {
            while (true)
            {
                transform.position += transform.up * MovementSpeed * Time.deltaTime;
                transform.Rotate(0, 0, Time.deltaTime * RotateSpeed);

                yield return new WaitForSeconds(Random.Range(0f, 5f));
            }
        }
    }
}
