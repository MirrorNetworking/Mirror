#if !UNITY_2019_2_OR_NEWER || UNITY_PERFORMANCE_TESTS_1_OR_OLDER
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

namespace Mirror.Tests.Performance
{
    [Category("Performance")]
    [Category("Benchmark")]
    public class NetworkIdentityPerformanceWithMultipleBehaviour
    {
        const int healthCount = 32;
        GameObject gameObject;
        NetworkIdentity identity;
        Health[] health;


        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
            identity.observers = new System.Collections.Generic.Dictionary<int, NetworkConnection>();
            identity.connectionToClient = new FakeNetworkConnection(1);
            identity.observers.Add(1, identity.connectionToClient);
            health = new Health[healthCount];
            for (int i = 0; i < healthCount; i++)
            {
                health[i] = gameObject.AddComponent<Health>();
                health[i].syncMode = SyncMode.Owner;
                health[i].syncInterval = 0f;
            }
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
        public void ServerUpdateIsDirty()
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
                for (int i = 0; i < healthCount; i++)
                {
                    health[i].SetDirtyBit(1UL);
                }
                identity.ServerUpdate();
            }
        }

        [Test]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceTest]
#endif
        public void ServerUpdateNotDirty()
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
