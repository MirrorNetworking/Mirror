using NUnit.Framework;
using UnityEngine.Experimental.LowLevel;
using UnityEngine.Experimental.PlayerLoop;

namespace Mirror.Tests
{
    public class NetworkLoopTests
    {
        // simple test to see if it finds and adds to EarlyUpdate() loop
        [Test]
        public void AddToPlayerLoop_EarlyUpdate_Beginning()
        {
            void Function() {}

            // get the loop and add our function
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
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

            // get the loop and add our function
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
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

            // get the loop and add our function
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
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

            // get the loop and add our function
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(Update), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);

            // was it added to the end? we don't know the exact index, but it should be >0
            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(Update));
            Assert.That(index, Is.GreaterThan(0));
        }

        [Test]
        public void AddToPlayerLoop_PostLateUpdate_Beginning()
        {
            void Function() {}

            // get the loop and add our function
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(PostLateUpdate), NetworkLoop.AddMode.Beginning);
            Assert.That(result, Is.True);

            // was it added to the beginning?
            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(PostLateUpdate));
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void AddToPlayerLoop_PostLateUpdate_End()
        {
            void Function() {}

            // get the loop and add our function
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(PostLateUpdate), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);

            // was it added to the end? we don't know the exact index, but it should be >0
            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(PostLateUpdate));
            Assert.That(index, Is.GreaterThan(0));
        }
    }
}
