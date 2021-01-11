using NSubstitute;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

namespace Mirror.Tests.Performance
{
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
            identity.ConnectionToClient = Substitute.For<INetworkConnection>();
            identity.observers.Add(identity.ConnectionToClient);
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
        [Performance]
        public void NetworkIdentityServerUpdateIsDirty()
        {
            Measure.Method(RunServerUpdateIsDirty)
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();
        }

        void RunServerUpdateIsDirty()
        {
            for (int j = 0; j < 1000; j++)
            {
                health.SetDirtyBit(1UL);
                identity.ServerUpdate();
            }
        }

        [Test]
        [Performance]
        public void NetworkIdentityServerUpdateNotDirty()
        {
            Measure.Method(RunServerUpdateNotDirty)
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();
        }

        void RunServerUpdateNotDirty()
        {
            for (int j = 0; j < 1000; j++)
            {
                identity.ServerUpdate();
            }
        }
    }
}

