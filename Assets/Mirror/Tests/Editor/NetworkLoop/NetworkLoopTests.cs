using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Mirror.Tests
{
    public class NetworkLoopTests
    {
        // all tests need a PlayerLoopSystem to work with
        PlayerLoopSystem playerLoop;

        [SetUp]
        public void SetUp()
        {
            // get the default player loop to work with.
            // we work on a local copy only - never call PlayerLoop.SetPlayerLoop here.
            playerLoop = PlayerLoop.GetDefaultPlayerLoop();
        }

        [TearDown]
        public void TearDown()
        {
            // reset static callbacks in case any test modified them
            NetworkLoop.OnEarlyUpdate = null;
            NetworkLoop.OnLateUpdate  = null;
        }

        // helper: traverse the player loop tree and return the subsystem count
        // for a given system type. returns null if the type is not found.
        static int? GetSubSystemCount(PlayerLoopSystem loop, Type systemType)
        {
            if (loop.type == systemType)
                return loop.subSystemList?.Length ?? 0;

            if (loop.subSystemList != null)
                foreach (PlayerLoopSystem sub in loop.subSystemList)
                {
                    int? count = GetSubSystemCount(sub, systemType);
                    if (count.HasValue) return count;
                }

            return null;
        }

        // helper: create a PlayerLoopSystem.UpdateFunction delegate for a
        // private static method on NetworkLoop (used for RuntimeInitializeOnLoad tests).
        static PlayerLoopSystem.UpdateFunction GetNetworkLoopDelegate(string methodName) =>
            (PlayerLoopSystem.UpdateFunction)Delegate.CreateDelegate(
                typeof(PlayerLoopSystem.UpdateFunction),
                typeof(NetworkLoop).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic));

        // helper: invoke a private static method on NetworkLoop via reflection.
        static void InvokePrivate(string methodName) =>
            typeof(NetworkLoop)
                .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);

        // =====================================================================
        // FindPlayerLoopEntryIndex
        // =====================================================================

        // function is present → returns its index
        [Test]
        public void FindPlayerLoopEntryIndex_Found()
        {
            void Function() {}
            NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.Beginning);

            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(EarlyUpdate));
            Assert.That(index, Is.EqualTo(0));
        }

        // function was never added → returns -1
        [Test]
        public void FindPlayerLoopEntryIndex_FunctionNotAdded_Returns_Negative1()
        {
            void Function() {}

            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(EarlyUpdate));
            Assert.That(index, Is.EqualTo(-1));
        }

        // type doesn't exist anywhere in the loop tree → returns -1
        [Test]
        public void FindPlayerLoopEntryIndex_TypeNotFound_Returns_Negative1()
        {
            void Function() {}

            // NetworkLoopTests is not a player loop system type
            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(NetworkLoopTests));
            Assert.That(index, Is.EqualTo(-1));
        }

        // =====================================================================
        // AddToPlayerLoop – EarlyUpdate
        // =====================================================================

        [Test]
        public void AddToPlayerLoop_EarlyUpdate_Beginning()
        {
            void Function() {}

            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.Beginning);
            Assert.That(result, Is.True);

            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(EarlyUpdate));
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void AddToPlayerLoop_EarlyUpdate_End()
        {
            void Function() {}

            // capture the pre-add count so we can assert the exact last position
            int initialCount = GetSubSystemCount(playerLoop, typeof(EarlyUpdate)).Value;

            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);

            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(EarlyUpdate));
            Assert.That(index, Is.EqualTo(initialCount)); // appended at the exact last position
        }

        // =====================================================================
        // AddToPlayerLoop – Update
        // =====================================================================

        [Test]
        public void AddToPlayerLoop_Update_Beginning()
        {
            void Function() {}

            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(Update), NetworkLoop.AddMode.Beginning);
            Assert.That(result, Is.True);

            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(Update));
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void AddToPlayerLoop_Update_End()
        {
            void Function() {}

            int initialCount = GetSubSystemCount(playerLoop, typeof(Update)).Value;

            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(Update), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);

            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(Update));
            Assert.That(index, Is.EqualTo(initialCount));
        }

        // =====================================================================
        // AddToPlayerLoop – PreLateUpdate
        // =====================================================================

        [Test]
        public void AddToPlayerLoop_PreLateUpdate_Beginning()
        {
            void Function() {}

            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(PreLateUpdate), NetworkLoop.AddMode.Beginning);
            Assert.That(result, Is.True);

            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(PreLateUpdate));
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void AddToPlayerLoop_PreLateUpdate_End()
        {
            void Function() {}

            int initialCount = GetSubSystemCount(playerLoop, typeof(PreLateUpdate)).Value;

            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(PreLateUpdate), NetworkLoop.AddMode.End);
            Assert.That(result, Is.True);

            int index = NetworkLoop.FindPlayerLoopEntryIndex(Function, playerLoop, typeof(PreLateUpdate));
            Assert.That(index, Is.EqualTo(initialCount));
        }

        // =====================================================================
        // AddToPlayerLoop – edge cases
        // =====================================================================

        // type not present anywhere in the player loop tree → returns false
        [Test]
        public void AddToPlayerLoop_TypeNotFound_Returns_False()
        {
            void Function() {}

            bool result = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(NetworkLoopTests), NetworkLoop.AddMode.End);
            Assert.That(result, Is.False);
        }

        // adding the same function a second time must not insert a duplicate entry.
        // covers the Array.FindIndex guard inside AddToPlayerLoop.
        [Test]
        public void AddToPlayerLoop_NoDuplicate_WhenAddedTwice()
        {
            void Function() {}

            int initialCount = GetSubSystemCount(playerLoop, typeof(EarlyUpdate)).Value;

            bool result1 = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.End);
            bool result2 = NetworkLoop.AddToPlayerLoop(Function, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.End);
            Assert.That(result1, Is.True);
            Assert.That(result2, Is.True); // second call still succeeds...

            // ...but the list grew by exactly 1, not 2
            int newCount = GetSubSystemCount(playerLoop, typeof(EarlyUpdate)).Value;
            Assert.That(newCount, Is.EqualTo(initialCount + 1));
        }

        // each successive Beginning insert must push the previous entry one slot right.
        // covers the Array.Copy shift logic.
        [Test]
        public void AddToPlayerLoop_Beginning_ShiftsExistingEntries()
        {
            void FunctionA() {}
            void FunctionB() {}

            // add A first, then B – both at the beginning
            NetworkLoop.AddToPlayerLoop(FunctionA, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.Beginning);
            NetworkLoop.AddToPlayerLoop(FunctionB, typeof(NetworkLoopTests), ref playerLoop, typeof(EarlyUpdate), NetworkLoop.AddMode.Beginning);

            int indexA = NetworkLoop.FindPlayerLoopEntryIndex(FunctionA, playerLoop, typeof(EarlyUpdate));
            int indexB = NetworkLoop.FindPlayerLoopEntryIndex(FunctionB, playerLoop, typeof(EarlyUpdate));

            // B was inserted last at the beginning, so B=0, A was shifted to 1
            Assert.That(indexB, Is.EqualTo(0));
            Assert.That(indexA, Is.EqualTo(1));
        }

        // =====================================================================
        // RuntimeInitializeOnLoad
        // =====================================================================

        // Verifies NetworkEarlyUpdate is appended to the end of EarlyUpdate.
        // Saves/restores the actual Unity player loop to avoid test pollution.
        [Test]
        public void RuntimeInitializeOnLoad_AddsNetworkEarlyUpdate_ToEndOfEarlyUpdate()
        {
            PlayerLoopSystem savedLoop = PlayerLoop.GetCurrentPlayerLoop();
            try
            {
                PlayerLoopSystem defaultLoop = PlayerLoop.GetDefaultPlayerLoop();
                int initialCount = GetSubSystemCount(defaultLoop, typeof(EarlyUpdate)).Value;
                PlayerLoop.SetPlayerLoop(defaultLoop);

                InvokePrivate("RuntimeInitializeOnLoad");

                int index = NetworkLoop.FindPlayerLoopEntryIndex(
                    GetNetworkLoopDelegate("NetworkEarlyUpdate"),
                    PlayerLoop.GetCurrentPlayerLoop(),
                    typeof(EarlyUpdate));

                Assert.That(index, Is.EqualTo(initialCount)); // appended at end
            }
            finally
            {
                PlayerLoop.SetPlayerLoop(savedLoop);
            }
        }

        // Verifies NetworkLateUpdate is appended to the end of PreLateUpdate.
        [Test]
        public void RuntimeInitializeOnLoad_AddsNetworkLateUpdate_ToEndOfPreLateUpdate()
        {
            PlayerLoopSystem savedLoop = PlayerLoop.GetCurrentPlayerLoop();
            try
            {
                PlayerLoopSystem defaultLoop = PlayerLoop.GetDefaultPlayerLoop();
                int initialCount = GetSubSystemCount(defaultLoop, typeof(PreLateUpdate)).Value;
                PlayerLoop.SetPlayerLoop(defaultLoop);

                InvokePrivate("RuntimeInitializeOnLoad");

                int index = NetworkLoop.FindPlayerLoopEntryIndex(
                    GetNetworkLoopDelegate("NetworkLateUpdate"),
                    PlayerLoop.GetCurrentPlayerLoop(),
                    typeof(PreLateUpdate));

                Assert.That(index, Is.EqualTo(initialCount)); // appended at end
            }
            finally
            {
                PlayerLoop.SetPlayerLoop(savedLoop);
            }
        }

        // =====================================================================
        // NetworkEarlyUpdate / NetworkLateUpdate – edit mode early-return branch
        // Application.isPlaying is always false in editor tests, so both methods
        // must return immediately without invoking any callbacks.
        // =====================================================================

        [Test]
        public void NetworkEarlyUpdate_DoesNothing_InEditMode()
        {
            bool called = false;
            NetworkLoop.OnEarlyUpdate += () => called = true;

            InvokePrivate("NetworkEarlyUpdate");

            Assert.That(called, Is.False);
        }

        [Test]
        public void NetworkLateUpdate_DoesNothing_InEditMode()
        {
            bool called = false;
            NetworkLoop.OnLateUpdate += () => called = true;

            InvokePrivate("NetworkLateUpdate");

            Assert.That(called, Is.False);
        }

        // =====================================================================
        // ResetStatics / callbacks
        // =====================================================================

        // ResetStatics is a private [RuntimeInitializeOnLoadMethod].
        // verify it nulls out both public callbacks.
        [Test]
        public void ResetStatics_ClearsOnEarlyUpdateAndOnLateUpdate()
        {
            NetworkLoop.OnEarlyUpdate = () => {};
            NetworkLoop.OnLateUpdate  = () => {};

            InvokePrivate("ResetStatics");

            Assert.That(NetworkLoop.OnEarlyUpdate, Is.Null);
            Assert.That(NetworkLoop.OnLateUpdate,  Is.Null);
        }
    }
}
