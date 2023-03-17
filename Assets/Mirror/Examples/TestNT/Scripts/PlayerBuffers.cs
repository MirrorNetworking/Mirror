using UnityEngine;
using Mirror;
using TMPro;

namespace TestNT
{
    public class PlayerBuffers : NetworkBehaviour
    {
        Transform mainCamTransform;

        [Header("Components")]
        public NTRCustomSendInterval NTRCustomSendInterval;
        public NetworkTransformReliable NetworkTransformReliable;
        public TextMeshPro clientBufferText;
        public TextMeshPro serverBufferText;
        public TextMeshPro localTimescaleText;
        public TextMeshPro snapIntText;

        [Header("Local Timescale Colors")]
        public Color catchupColor = Color.red;
        public Color slowdownColor = Color.blue;

        [Header("Diagnostics - Do Not Modify")]
        public int serverSnapCount;
        public int clientSnapCount;

        private void OnValidate()
        {
            NTRCustomSendInterval = GetComponent<NTRCustomSendInterval>();
            NetworkTransformReliable = GetComponent<NetworkTransformReliable>();

            // Force overrideColorTags true so we can change the color without tags
            clientBufferText.overrideColorTags = true;
            serverBufferText.overrideColorTags = true;

            this.enabled = false;
        }

        public override void OnStartClient()
        {
            mainCamTransform = Camera.main.transform;
            this.enabled = true;
        }

        public override void OnStopClient()
        {
            this.enabled = false;
        }

        void Update()
        {
            /////// Client
            if (NTRCustomSendInterval)
                clientSnapCount = NTRCustomSendInterval.clientSnapshots.Count;
            if (NetworkTransformReliable)
                clientSnapCount = NetworkTransformReliable.clientSnapshots.Count;

            if (clientSnapCount < 2)
                clientBufferText.color = Color.black;
            else if (clientSnapCount < 3)
                clientBufferText.color = Color.green;
            else if (clientSnapCount < 4)
                clientBufferText.color = Color.yellow;
            else
                clientBufferText.color = Color.red;

            clientBufferText.text = $"C: {new string('-', clientSnapCount)}";

            if (NetworkClient.localTimescale < 0)
                localTimescaleText.color = slowdownColor;
            else if (NetworkClient.localTimescale > 0)
                localTimescaleText.color = catchupColor;
            else
                localTimescaleText.color = Color.black;

            localTimescaleText.text = $"LT: {NetworkClient.localTimescale:0.0000}";

            /////// Server
            //serverSnapCount = networkTransformReliable.serverSnapshots.Count;

            //if (serverSnapCount < 2)
            //    serverBufferText.color = Color.gray;
            //else if (serverSnapCount < 3)
            //    serverBufferText.color = Color.green;
            //else if (serverSnapCount < 4)
            //    serverBufferText.color = Color.yellow;
            //else
            //    serverBufferText.color = Color.red;

            //serverBufferText.text = "S: " + new string('-', serverSnapCount);

            /////// Snap Interpolation
            //snapIntText.text = $"{networkTransformReliable.velocity.magnitude:N2}" +
            //                 $"\n{transform.position}";
        }

        void LateUpdate()
        {
            clientBufferText.transform.forward = mainCamTransform.forward;
            serverBufferText.transform.forward = mainCamTransform.forward;
            localTimescaleText.transform.forward = mainCamTransform.forward;
            snapIntText.transform.forward = mainCamTransform.forward;
        }
    }
}
