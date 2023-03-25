using System;
using UnityEngine;
using TMPro;

namespace TestNT
{
    public class PlayerHeight : MonoBehaviour
    {
        Transform mainCamTransform;
        float maxHeight;

        [Header("Components")]
        public TextMeshPro playerHeightText;

        void Awake()
        {
            mainCamTransform = Camera.main.transform;
        }

        void Update()
        {
            // subtract half height and skin width
            maxHeight = Mathf.Max(maxHeight, transform.position.y - 1.02f);
            playerHeightText.text = $"{MathF.Round(maxHeight, 2)}";
        }

        void LateUpdate()
        {
            playerHeightText.transform.forward = mainCamTransform.forward;
        }
    }
}
