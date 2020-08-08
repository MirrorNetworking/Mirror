#if !UNITY_2019_2_OR_NEWER || UNITY_PERFORMANCE_TESTS_1_OR_OLDER
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
    public class NetworkIdentityServerUpdatePerformance
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


    [Category("Performance")]
    [Category("Benchmark")]
    public class NetworkIdentityEqualsPerformance
    {
        GameObject gameObject1;
        NetworkIdentity identity1;
        Object obj1;

        GameObject gameObject2;
        NetworkIdentity identity2;
        Object obj2;


        [SetUp]
        public void SetUp()
        {
            gameObject1 = new GameObject();
            identity1 = gameObject1.AddComponent<NetworkIdentity>();
            obj1 = identity1;

            gameObject2 = new GameObject();
            identity2 = gameObject2.AddComponent<NetworkIdentity>();
            obj2 = identity2;
        }
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(gameObject1);
            UnityEngine.Object.DestroyImmediate(gameObject2);
        }

        [Test]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceTest]
#endif
        public void EqualsPerformance()
        {
            const int count = 1000;
            const int warmup = count / 100;
            const SampleUnit sampleUnit = SampleUnit.Microsecond;


            Measure.Method(checkId)
              .Definition(name: "Custom".PadRight(20), sampleUnit: sampleUnit)
              .WarmupCount(warmup)
              .MeasurementCount(count)
              .Run();


            Measure.Method(checkObj)
              .Definition(name: "Unity".PadRight(20), sampleUnit: sampleUnit)
              .WarmupCount(warmup)
              .MeasurementCount(count)
              .Run();

            Measure.Method(checkIsDestroyed)
              .Definition(name: "IsDestroyed".PadRight(20), sampleUnit: sampleUnit)
              .WarmupCount(warmup)
              .MeasurementCount(count)
              .Run();

            Measure.Method(checkIsNotDestroyed)
             .Definition(name: "IsNotDestroyed".PadRight(20), sampleUnit: sampleUnit)
             .WarmupCount(warmup)
             .MeasurementCount(count)
             .Run();

            Measure.Method(checkIsNull)
             .Definition(name: "IsNull".PadRight(20), sampleUnit: sampleUnit)
             .WarmupCount(warmup)
             .MeasurementCount(count)
             .Run();

            Measure.Method(checkAreEqual)
             .Definition(name: "AreEqual".PadRight(20), sampleUnit: sampleUnit)
             .WarmupCount(warmup)
             .MeasurementCount(count)
             .Run();
        }

        private void checkId()
        {
            int x = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (identity1 != null)
                {
                    // do stuff here
                    x++;
                }
            }
        }
        private void checkObj()
        {
            int x = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (obj1 != null)
                {
                    // do stuff here
                    x++;
                }
            }
        }
        private void checkIsDestroyed()
        {
            int x = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (!identity1.IsDestroyed)
                {
                    // do stuff here
                    x++;
                }
            }
        }
        private void checkIsNotDestroyed()
        {
            int x = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (identity1.IsNotDestroyed)
                {
                    // do stuff here
                    x++;
                }
            }
        }
        private void checkIsNull()
        {
            int x = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (identity1 is null)
                {
                    // do stuff here
                    x++;
                }
            }
        }
        private void checkAreEqual()
        {
            int x = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (NetworkIdentity.AreEqual(identity1, null))
                {
                    // do stuff here
                    x++;
                }
            }
        }



        [Test]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceTest]
#endif
        public void NullCheckPerformance()
        {
            const int count = 10000;
            const int warmup = count / 100;
            const SampleUnit sampleUnit = SampleUnit.Microsecond;

            Measure.Method(CheckIsNull)
              .Definition(name: "IsNull".PadRight(20), sampleUnit: sampleUnit)
              .WarmupCount(warmup)
              .MeasurementCount(count)
              .Run();


            Measure.Method(CheckEqualsNull)
              .Definition(name: "EqualsNull".PadRight(20), sampleUnit: sampleUnit)
              .WarmupCount(warmup)
              .MeasurementCount(count)
              .Run();

            Measure.Method(CheckUnityEqualsNull)
              .Definition(name: "UnityEqualsNull".PadRight(20), sampleUnit: sampleUnit)
              .WarmupCount(warmup)
              .MeasurementCount(count)
              .Run();

            Measure.Method(CheckCompIsNull)
             .Definition(name: "CompIsNull".PadRight(20), sampleUnit: sampleUnit)
             .WarmupCount(warmup)
             .MeasurementCount(count)
             .Run();

            Measure.Method(CheckRefEquals)
             .Definition(name: "RefEquals".PadRight(20), sampleUnit: sampleUnit)
             .WarmupCount(warmup)
             .MeasurementCount(count)
             .Run();
        }

        private void CheckIsNull()
        {
            int x = 0;
            NetworkIdentity id = gameObject1.GetComponent<NetworkIdentity>();
            for (int i = 0; i < 100; i++)
            {
                if (id is null)
                {
                    // do stuff here
                    x++;
                }
            }
        }
        private void CheckEqualsNull()
        {
            int x = 0;
            NetworkIdentity id = gameObject1.GetComponent<NetworkIdentity>();
            for (int i = 0; i < 100; i++)
            {
                if (id == null)
                {
                    // do stuff here
                    x++;
                }
            }
        }
        private void CheckUnityEqualsNull()
        {
            int x = 0;
            Component id = gameObject1.GetComponent(typeof(NetworkIdentity));
            for (int i = 0; i < 100; i++)
            {
                if (id == null)
                {
                    // do stuff here
                    x++;
                }
            }
        }
        private void CheckCompIsNull()
        {
            int x = 0;
            Component id = gameObject1.GetComponent(typeof(NetworkIdentity));
            for (int i = 0; i < 100; i++)
            {
                if (id is null)
                {
                    // do stuff here
                    x++;
                }
            }
        }
        private void CheckRefEquals()
        {
            int x = 0;
            NetworkIdentity id = gameObject1.GetComponent<NetworkIdentity>();
            for (int i = 0; i < 100; i++)
            {
                if (object.ReferenceEquals(id, null))
                {
                    // do stuff here
                    x++;
                }
            }
        }



        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void IsNotNull(bool optimized)
        {
            bool check;
            if (optimized)
            {
                check = identity1 != null;
            }
            else
            {
                check = obj1 != null;
            }
            Assert.IsTrue(check);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void NotEqualCheck(bool optimized)
        {
            bool check;
            if (optimized)
            {
                check = identity1 != identity2;
            }
            else
            {
                check = obj1 != obj2;
            }
            Assert.IsTrue(check);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void EqualCheck(bool optimized)
        {
            bool check;
            if (optimized)
            {
                check = identity1 == identity2;
            }
            else
            {
                check = obj1 == obj2;
            }
            Assert.IsFalse(check);
        }


        [Test]
        public void GetComponentShouldReturnNull()
        {
            GameObject go = new GameObject();

            // add comp
            TestBehaviour test1 = go.AddComponent<TestBehaviour>();
            // Destroy comp
            GameObject.DestroyImmediate(test1);

            // get destroyed comp
            TestBehaviour getDestroyed = go.GetComponent<TestBehaviour>();
            // destroyed comp should be null
            Assert.IsTrue(getDestroyed is null);

            GameObject.DestroyImmediate(go);
        }
    }

    public class TestBehaviour : MonoBehaviour
    {

    }
}
#endif
