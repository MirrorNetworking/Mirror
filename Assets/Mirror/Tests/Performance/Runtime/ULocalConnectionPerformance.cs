#if !UNITY_2019_2_OR_NEWER || UNITY_PERFORMANCE_TESTS_1_OR_OLDER
using System.Collections;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;


namespace Mirror.Tests.Performance.Runtime
{
    class NetworkManagerTest : NetworkManager
    {
        public override void Awake()
        {
            transport = gameObject.AddComponent<MemoryTransport>();
            playerPrefab = new GameObject();
            NetworkIdentity identity = playerPrefab.AddComponent<NetworkIdentity>();
            identity.assetId = System.Guid.NewGuid();
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

        IEnumerator SetUpConnections()
        {
            GameObject go = new GameObject();
            manager = go.AddComponent<NetworkManagerTest>();
            yield return null;

            manager.StartHost();

            yield return null;
        }
        IEnumerator Disconnect()
        {
            manager.StopHost();
            yield return null;
            GameObject.Destroy(manager.gameObject);
        }
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (NetworkManager.singleton != null)
            {
                GameObject go = NetworkManager.singleton.gameObject;
                NetworkManager.Shutdown();
                GameObject.DestroyImmediate(go);
            }
        }

        [UnityTest]
        public IEnumerator ConnectAndDisconnectWorks()
        {
            yield return SetUpConnections();

            yield return Disconnect();
        }
        [UnityTest]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceUnityTest]
#endif
        public IEnumerator ULocalConnectionPerformanceWithEnumeratorPasses()
        {
            yield return SetUpConnections();

            using (Measure.Frames()
                .WarmupCount(10)
                .MeasurementCount(100)
                .Scope())
            {
                for (int i = 0; i < 100; i++)
                {
                    sendSomeMessages();
                    yield return null;
                }
            }

            yield return Disconnect();
        }

        static void sendSomeMessages()
        {
            for (uint i = 0; i < 10000u; i++)
            {
                using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
                {
                    // write mask
                    writer.WriteUInt64(1);
                    // behaviour length
                    writer.WriteInt32(1);

                    // behaviour delta

                    //  sync object mask
                    writer.WriteUInt64(0);
                    // sync object delta
                    //      assume no sync objects for this test

                    // sync var mask
                    writer.WriteUInt64(1);
                    // sync var delta
                    //      assume sync var has changed its value to 10
                    writer.WriteInt32(10);


                    // send message
                    NetworkServer.localConnection.Send(new UpdateVarsMessage
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
