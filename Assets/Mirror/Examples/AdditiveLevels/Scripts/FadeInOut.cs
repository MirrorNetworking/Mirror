using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.AdditiveLevels
{
    public class FadeInOut : MonoBehaviour
    {
        // set these in the inspector
        [Range(1, 100), Tooltip("Speed of fade in / out: lower is slower")]
        public byte speed = 1;

        [Tooltip("Reference to Image component on child panel")]
        public Image fadeImage;

        [Tooltip("Color to use during scene transition")]
        public Color fadeColor = Color.black;

        WaitForSeconds waitForSeconds;

        void Awake()
        {
            waitForSeconds = new WaitForSeconds(speed * 0.01f);
        }

        public IEnumerator FadeIn()
        {
            float alpha = fadeImage.color.a;

            while (alpha < 1)
            {
                yield return waitForSeconds;
                alpha += 0.01f;
                fadeColor.a = alpha;
                fadeImage.color = fadeColor;
            }
        }

        public IEnumerator FadeOut()
        {
            float alpha = fadeImage.color.a;

            while (alpha > 0)
            {
                yield return waitForSeconds;
                alpha -= 0.01f;
                fadeColor.a = alpha;
                fadeImage.color = fadeColor;
            }
        }
    }
}
