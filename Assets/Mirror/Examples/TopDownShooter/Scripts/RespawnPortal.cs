using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnPortal : MonoBehaviour
{
    public float rotationSpeed = 360f; // Degrees per second
    public float shrinkDuration = 1f;  // Time in seconds to shrink to zero
    public AudioSource soundEffect;

    private Vector3 originalScale;
    private float shrinkTimer;
    

    void Awake()
    {
        // Store the original scale of the portal
        originalScale = transform.localScale;
        // Initialize the shrink timer
        shrinkTimer = shrinkDuration;
    }

    void OnEnable()
    {
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
}
