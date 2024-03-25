using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
namespace Mirror.Examples.DataBenchmark
{
    public class BenchmarkSender : NetworkBehaviour
    {

        public int DataSize = 256;
        public int SendsPerFrame = 100;
        [ReadOnly]
        public int DataPerFrame;

        public bool Reliable = true;
        private byte[] data;
        private void Awake()
        {
            data = new byte[DataSize];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }
        }
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (!NetworkServer.active)
            {
                return;
            }
            for (int i = 0; i < SendsPerFrame; i++)
            {
                if (Reliable)
                {
                    RpcSendReliable(new ArraySegment<byte>(data));
                }
                else
                {
                    RpcSendUnreliable(new ArraySegment<byte>(data));
                }
            }
        }


        [ClientRpc(channel = Channels.Reliable)]
        void RpcSendReliable(ArraySegment<byte> data)
        {

        }

        [ClientRpc(channel = Channels.Unreliable)]
        void RpcSendUnreliable(ArraySegment<byte> data)
        {

        }

        protected override void OnValidate()
        {
            base.OnValidate();
            DataPerFrame = DataSize * SendsPerFrame;
        }
    }
}
