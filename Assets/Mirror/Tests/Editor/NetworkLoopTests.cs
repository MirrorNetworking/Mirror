using NUnit.Framework;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
#else
using UnityEngine.Experimental.LowLevel;
using UnityEngine.Experimental.PlayerLoop;
#endif

namespace Mirror.Tests
{
    public class NetworkLoopTests
    {
        // all tests need a PlayerLoopSystem to work with
        PlayerLoopSystem playerLoop;

        [SetUp]
        public void SetUp()
        {
            // we get the main player loop to work with.
            // we don't actually set it. no need to.
            playerLoop = PlayerLoop.GetDefaultPlayerLoop();
        }

        // simple test to see if it finds and adds to EarlyUpdate() loop
        [Test]
        public void AddToPlayerLoop_EarlyUpdate_Beginning()
        {
            void Function() {}

            // add our function
            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.Beginning);
            Assert.That(result, Is.True);

            // was it added to the beginning?
            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(EarlyUpdate));
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void AddToPlayerLoop_EarlyUpdate_End()
        {
            void Function() {}

            // add our function
            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);

            // was it added to the end? we don't know the exact index, but it should be >0
            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(EarlyUpdate));
            Assert.That(index, Is.GreaterThan(0));
        }

        [Test]
        public void AddToPlayerLoop_Update_Beginning()
        {
            void Function() {}

            // add our function
            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(Update), NetworkLoop.AddMode.Beginning);
            Assert.That(result, Is.True);

            // was it added to the beginning?
            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(Update));
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void AddToPlayerLoop_Update_End()
        {
            void Function() {}

            // add our function
            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(Update), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);

            // was it added to the end? we don't know the exact index, but it should be >0
            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(Update));
            Assert.That(index, Is.GreaterThan(0));
        }

        [Test]
        public void AddToPlayerLoop_PreLateUpdate_Beginning()
        {
            void Function() {}

            // add our function
            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(PreLateUpdate), NetworkLoop.AddMode.Beginning);
            Assert.That(result, Is.True);

            // was it added to the beginning?
            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(PreLateUpdate));
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void AddToPlayerLoop_PreLateUpdate_End()
        {
            void Function() {}

            // add our function
            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(PreLateUpdate), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);

            // was it added to the end? we don't know the exact index, but it should be >0
            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(PreLateUpdate));
            Assert.That(index, Is.GreaterThan(0));
        }
    }
}
