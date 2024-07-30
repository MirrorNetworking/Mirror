// source: Unity Standard Assets - Angry Bots (Unity Companion License).
// Modifications by Mirror.
using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class MuzzleFlash : MonoBehaviour
    {
        public float visibleTime = 0.1f;
        public bool rotate = true;
        float endTime;
        Vector3 originalScale;

        void Awake()
        {
            originalScale = transform.localScale;
        }

        void FixedUpdate()
        {
            transform.localScale = originalScale * Random.Range(0.5f, 1.5f);
            if (rotate) transform.Rotate(0, 0, Random.Range(0f, 90f));

            // hide self after end time. we don't use Invoke because it might be
            // enabled over and over again
            if (Time.time >= endTime) gameObject.SetActive(false);
        }

        public void Fire()
        {
            // reset end time
            gameObject.SetActive(true);
            endTime = Time.time + visibleTime;
        }
    }
}
