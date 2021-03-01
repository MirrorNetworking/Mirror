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
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddToPlayerLoop(() => {}, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.Beginning);
            Assert.That(result, Is.True);
        }

        [Test]
        public void AddToPlayerLoop_EarlyUpdate_End()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddToPlayerLoop(() => {}, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);
        }

        // simple test to see if it finds and adds to Update() loop
        [Test]
        public void AddToPlayerLoop_Update_Beginning()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddToPlayerLoop(() => {}, typeof(NetworkLoopTests), ref playerLoop, typeof(Update), NetworkLoop.AddMode.Beginning);
            Assert.That(result, Is.True);
        }

        [Test]
        public void AddToPlayerLoop_Update_End()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddToPlayerLoop(() => {}, typeof(NetworkLoopTests), ref playerLoop, typeof(Update), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);
        }

        // simple test to see if it finds and adds to PostLateUpdate() loop
        [Test]
        public void AddToPlayerLoop_PostLateUpdate_Beginning()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddToPlayerLoop(() => {}, typeof(NetworkLoopTests), ref playerLoop, typeof(PostLateUpdate), NetworkLoop.AddMode.Beginning);
            Assert.That(result, Is.True);
        }

        [Test]
        public void AddToPlayerLoop_PostLateUpdate_End()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddToPlayerLoop(() => {}, typeof(NetworkLoopTests), ref playerLoop, typeof(PostLateUpdate), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);
        }
    }
}
