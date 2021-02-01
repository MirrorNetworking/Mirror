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

        public static WaitForSeconds wait1Sec;
        public static float waitAmount;

        public void Start()
        {
            wait1Sec = new WaitForSeconds(waitAmount);
            StartCoroutine(Move());
        }

        private IEnumerator Move()
        {
            while (true)
            {
                transform.position += transform.up * MovementSpeed * Time.deltaTime;
                transform.Rotate(0, 0, Time.deltaTime * RotateSpeed);

                waitAmount = Random.Range(0, 0.5f);
                yield return wait1Sec;
            }
        }
    }
}
