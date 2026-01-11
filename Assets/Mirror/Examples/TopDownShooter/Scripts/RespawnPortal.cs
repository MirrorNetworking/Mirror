using System.Collections;
using UnityEngine;

namespace Mirror.Examples.TopDownShooter
{
    [AddComponentMenu("")]
    public class RespawnPortal : MonoBehaviour
    {
        public float rotationSpeed = 360f; // Degrees per second
        public float shrinkDuration = 1f;  // Time in seconds to shrink to zero
        public AudioSource soundEffect;

        private Vector3 originalScale;
        private float shrinkTimer;

#if !UNITY_SERVER
        void Awake()
        {
            // Store the original setup
            originalScale = transform.localScale;
            shrinkTimer = shrinkDuration;
        }

        void OnEnable()
        {
            // By using OnEnable, it shortcuts the function to be called automatically when gameobject is SetActive false/true.
            // Here we reset variables, and then call the Portal respawn effect
            transform.localScale = originalScale;
            shrinkTimer = shrinkDuration;

            StartCoroutine(StartEffect());
        }

        IEnumerator StartEffect()
        {
            soundEffect.Play();
            while (shrinkTimer > 0)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

                if (shrinkTimer > 0)
                {
                    shrinkTimer -= Time.deltaTime;
                    float scale = Mathf.Clamp01(shrinkTimer / shrinkDuration);
                    transform.localScale = originalScale * scale;

                    yield return new WaitForEndOfFrame();
                }
            }
        }
#endif
    }
}