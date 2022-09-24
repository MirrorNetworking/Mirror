using UnityEngine;

namespace Mirror.Examples.CCU
{
    public class Monster : NetworkBehaviour
    {
        public float speed = 1;
        public float movementProbability = 0.5f;
        public float movementDistance = 20;

        bool moving;
        Vector3 start;
        Vector3 destination;

        // cache .transform for benchmark demo.
        // Component.get_transform shows in profiler otherwise.
        Transform tf;

        public override void OnStartServer()
        {
            tf = transform;
            start = tf.position;
        }

        [ServerCallback]
        void Update()
        {
            if (moving)
            {
                if (Vector3.Distance(tf.position, destination) <= 0.01f)
                {
                    moving = false;
                }
                else
                {
                    tf.position = Vector3.MoveTowards(tf.position, destination, speed * Time.deltaTime);
                }
            }
            else
            {
                float r = Random.value;
                if (r < movementProbability * Time.deltaTime)
                {
                    Vector2 circlePos = Random.insideUnitCircle;
                    Vector3 dir = new Vector3(circlePos.x, 0, circlePos.y);

                    // set destination on random pos in a circle around start.
                    // (don't want to wander off)
                    destination = start + dir * movementDistance;
                    moving = true;
                }
            }
        }
    }
}
