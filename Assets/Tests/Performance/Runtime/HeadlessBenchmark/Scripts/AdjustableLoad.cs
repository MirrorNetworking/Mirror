using System.Collections;
using UnityEngine;

namespace Mirror.HeadlessBenchmark
{
    public class AdjustableLoad : NetworkBehaviour
    {
        private float movementDuration = 5.0f;
        private float waitBeforeMoving = 5.0f;
        private bool hasArrived = false;

        private void Update()
        {
            if (!IsLocalPlayer)
                return;

            if (!hasArrived)
            {
                hasArrived = true;
                float randX = Random.Range(-15.0f, 15.0f);
                float randY = Random.Range(-15.0f, 15.0f);
                StartCoroutine(MoveToPoint(new Vector3(randX, randY, 0)));
            }
        }

        private IEnumerator MoveToPoint(Vector3 targetPos)
        {
            float timer = 0.0f;
            Vector3 startPos = transform.position;

            while (timer < movementDuration)
            {
                timer += Time.deltaTime;
                float t = timer / movementDuration;
                t = t * t * t * (t * (6f * t - 15f) + 10f);
                transform.position = Vector3.Lerp(startPos, targetPos, t);

                yield return null;
            }

            yield return new WaitForSeconds(waitBeforeMoving);
            hasArrived = false;
        }
    }
}
