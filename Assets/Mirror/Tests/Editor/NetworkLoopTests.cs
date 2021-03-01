using NUnit.Framework;
using UnityEngine.Experimental.LowLevel;
using UnityEngine.Experimental.PlayerLoop;

namespace Mirror.Tests
{
    public class NetworkLoopTests
    {
        // simple test to see if it finds and adds to EarlyUpdate() loop
        [Test]
        public void AddSystemToPlayerLoopList_EarlyUpdate()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddSystemToPlayerLoopList(() => {}, ref playerLoop, typeof(EarlyUpdate));
            Assert.That(result, Is.True);
        }

        // simple test to see if it finds and adds to Update() loop
        [Test]
        public void AddSystemToPlayerLoopList_Update()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddSystemToPlayerLoopList(() => {}, ref playerLoop, typeof(Update));
            Assert.That(result, Is.True);
        }

        // simple test to see if it finds and adds to PostLateUpdate() loop
        [Test]
        public void AddSystemToPlayerLoopList_PostLateUpdate()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            bool result = NetworkLoop.AddSystemToPlayerLoopList(() => {}, ref playerLoop, typeof(PostLateUpdate));
            Assert.That(result, Is.True);
        }
    }
}
