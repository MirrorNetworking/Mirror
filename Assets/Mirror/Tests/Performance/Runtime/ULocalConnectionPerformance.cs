#if !UNITY_2019_2_OR_NEWER || UNITY_PERFORMANCE_TESTS_1_OR_OLDER
using System.Collections;
using System.Threading.Tasks;
using Mirror;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests.Performance.Runtime
{

    class NetworkManagerTest : NetworkManager
    {
        public override void Awake()
        {
            transport = gameObject.AddComponent<MemoryTransport>();
            playerPrefab = new GameObject("testPlayerPrefab", typeof(NetworkIdentity));
            base.Awake();
        }
        public override void OnDestroy()
        {
            base.OnDestroy();

            // clean up new object created in awake
            Destroy(playerPrefab);
        }
    }

    [Category("Performance")]
    public class ULocalConnectionPerformance
    {
        NetworkManager manager;
        private GameObject playerPrefab;
        private NetworkServer server;
        private NetworkClient client;


        [UnitySetUp]
        public IEnumerator SetUp() => RunAsync(async () =>
        {
            var go = new GameObject();
            server = go.AddComponent<NetworkServer>();
            client = go.AddComponent<NetworkClient>();
            manager = go.AddComponent<NetworkManager>();
            manager.client = client;
            manager.server = server;
            manager.startOnHeadless = false;

            playerPrefab = new GameObject("testPlayerPrefab", typeof(NetworkIdentity));

            await manager.StartHost();
        });

        [TearDown]
        public void Disconnect()
        {
            manager.StopHost();
            GameObject.Destroy(playerPrefab);
            GameObject.Destroy(manager.gameObject);
        }
        
        [UnityTest]
        [Performance]
        public IEnumerator ULocalConnectionPerformanceWithEnumeratorPasses()
        {

            using (Measure.Frames()
                .WarmupCount(10)
                .MeasurementCount(100)
                .Scope())
            {
                for (int i = 0; i < 100; i++)
                {
                    SendSomeMessages();
                    yield return null;
                }
            }
        }

        void SendSomeMessages()
        {
            for (uint i = 0; i < 10000u; i++)
            {
                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    // write mask
                    writer.WritePackedUInt64(1);
                    // behaviour length
                    writer.WriteInt32(1);

                    // behaviour delta

                    //  sync object mask
                    writer.WritePackedUInt64(0);
                    // sync object delta
                    //      assume no sync objects for this test

                    // sync var mask
                    writer.WritePackedUInt64(1);
                    // sync var delta
                    //      assume sync var has changed its value to 10
                    writer.WritePackedInt32(10);

                    // send message
                    server.LocalConnection.Send(new UpdateVarsMessage
                    {
                        netId = i,
                        payload = writer.ToArraySegment()
                    });
                }
            }
        }
    }
}
#endif
