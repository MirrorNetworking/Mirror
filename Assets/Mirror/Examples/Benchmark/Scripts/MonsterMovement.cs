using UnityEngine;

namespace Mirror.Examples.Benchmark
{
    public class MonsterMovement : NetworkBehaviour
    {
        public float speed = 1;

        // movement probability:
        // 0.5 is too high, monsters are moving almost all the time.
        // only-sync-on-change shows no difference with 0.5 at all.
        // in other words: broken change detection would be too easy to miss!
        [Header("Note: use 0.1 to test change detection, 0.5 is too high!")]
        public float movementProbability = 0.1f;
        public float movementDistance = 20;

        bool moving;
        Vector3 start;
        Vector3 destination;

        public override void OnStartServer()
        {
            start = transform.position;
        }

        [ServerCallback]
        void Update()
        {
            if (moving)
            {
                if (Vector3.Distance(transform.position, destination) <= 0.01f)
                {
                    transform.position = destination;
                    moving = false;
                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
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
