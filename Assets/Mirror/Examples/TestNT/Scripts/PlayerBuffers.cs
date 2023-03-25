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
        public NetworkTransform NetworkTransform;
        public NetworkTransformReliable NetworkTransformReliable;
        public TextMeshPro clientBufferText;
        public TextMeshPro serverBufferText;
        public TextMeshPro snapIntText;

        [Header("Diagnostics - Do Not Modify")]
        public int serverSnapCount;
        public int clientSnapCount;

        private void OnValidate()
        {
            NTRCustomSendInterval = GetComponent<NTRCustomSendInterval>();
            NetworkTransform = GetComponent<NetworkTransform>();
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
            if (NetworkTransform)
                clientSnapCount = NetworkTransform.clientSnapshots.Count;
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
            snapIntText.transform.forward = mainCamTransform.forward;
        }
    }
}
