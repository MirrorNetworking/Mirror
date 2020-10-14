using UnityEngine;

namespace Mirror.HeadlessBenchmark
{
    public class AdjustableLoad : NetworkBehaviour
    {
        public float MovementSpeed = 1;
        public float RotateSpeed = 50;

        [SyncVar]
        public float Floaty;

        // Update is called once per frame
        void Update()
        {
            if(HasAuthority)
            {
                transform.position += transform.up * MovementSpeed * Time.deltaTime;
                transform.Rotate(0, 0, Time.deltaTime * RotateSpeed);
            }

            if(IsServer)
            {
                transform.position += transform.up * MovementSpeed * Time.deltaTime;
                transform.Rotate(0, 0, Time.deltaTime * RotateSpeed);

                Floaty = Random.value;
            }
        }
    }
}
