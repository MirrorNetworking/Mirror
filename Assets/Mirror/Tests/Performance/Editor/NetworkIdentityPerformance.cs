#if !UNITY_2019_2_OR_NEWER || UNITY_PERFORMANCE_TESTS_1_OR_OLDER
using System;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

namespace Mirror.Tests.Performance
{
    public class FakeNetworkConnection : NetworkConnectionToClient
    {
        public FakeNetworkConnection(int networkConnectionId) : base(networkConnectionId)
        {
        }

        public override string address => "Test";

        public override void Disconnect()
        {
            // nothing
        }

        internal override bool Send(ArraySegment<byte> segment, int channelId = 0)
        {
            return true;
        }
    }
    public class Health : NetworkBehaviour
    {
        [SyncVar] public int health = 10;

        public void Update()
        {
            health = (health + 1) % 10;
        }
    }
    [Category("Performance")]
    [Category("Benchmark")]
    public class NetworkIdentityPerformance
    {
        GameObject gameObject;
        NetworkIdentity identity;
        Health health;


        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
            identity.observers = new System.Collections.Generic.Dictionary<int, NetworkConnection>();
            identity.connectionToClient = new FakeNetworkConnection(1);
            identity.observers.Add(1, identity.connectionToClient);
            health = gameObject.AddComponent<Health>();
            health.syncMode = SyncMode.Owner;
            health.syncInterval = 0f;
        }
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceTest]
#endif
        public void NetworkIdentityServerUpdateIsDirty()
        {
            Measure.Method(RunServerUpdateIsDirty)
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();
        }

        void RunServerUpdateIsDirty()
        {
            for (int j = 0; j < 10000; j++)
            {
                health.SetDirtyBit(1UL);
                identity.ServerUpdate();
            }
        }

        [Test]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceTest]
#endif
        public void NetworkIdentityServerUpdateNotDirty()
        {
            Measure.Method(RunServerUpdateNotDirty)
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();
        }

        void RunServerUpdateNotDirty()
        {
            for (int j = 0; j < 10000; j++)
            {
                identity.ServerUpdate();
            }
        }
    }
}
#endif
