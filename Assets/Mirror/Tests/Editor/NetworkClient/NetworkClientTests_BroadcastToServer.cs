using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_BroadcastToServer : MirrorEditModeTest
    {
        // Minimal NetworkBehaviour used to configure syncDirection / syncMethod per-test.
        // syncInterval is set to 0 automatically by CreateNetworked<T>.
        class BroadcastTestBehaviour : NetworkBehaviour { }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);
            // Broadcast() returns early when NetworkServer.active is true (host-mode guard).
            // Disable it so BroadcastToServer() is actually reached.
            NetworkServer.active = false;
        }

        [TearDown]
        public override void TearDown()
        {
            // Restore before base.TearDown so NetworkServer.Shutdown() runs cleanly.
            NetworkServer.active = true;
            base.TearDown();
        }

        // ---- helpers -------------------------------------------------------

        // Backdates lastUnreliableBaselineTime so AccurateInterval.Elapsed returns true.
        static void ForceBaselineElapsed()
        {
            FieldInfo field = typeof(NetworkClient).GetField(
                "lastUnreliableBaselineTime",
                BindingFlags.Static | BindingFlags.NonPublic);
            field.SetValue(null, double.MinValue);
        }

        // Advances lastUnreliableBaselineTime far into the future so the interval never elapses.
        static void PreventBaselineElapsed()
        {
            FieldInfo field = typeof(NetworkClient).GetField(
                "lastUnreliableBaselineTime",
                BindingFlags.Static | BindingFlags.NonPublic);
            field.SetValue(null, double.MaxValue);
        }

        // ---- tests ---------------------------------------------------------

        // Branch: connection.owned is empty — BroadcastToServer iterates nothing.
        [Test]
        public void BroadcastToServer_NoOwnedObjects_DoesNotThrow()
        {
            Assert.That(NetworkClient.connection.owned.Count, Is.EqualTo(0));
            Assert.DoesNotThrow(() => NetworkClient.NetworkLateUpdate());
        }

        // Branch: identity in owned was destroyed — logs warning and skips serialization.
        [Test]
        public void BroadcastToServer_NullIdentityInOwned_LogsWarning()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity);
            NetworkClient.connection.owned.Add(identity);

            // Destroy the GameObject so identity becomes Unity-null while still in the HashSet.
            GameObject.DestroyImmediate(go);

            LogAssert.Expect(LogType.Warning,
                "Found 'null' entry in owned list for client. This is unexpected behaviour.");

            NetworkClient.NetworkLateUpdate();
        }

        // Branch: writerReliable.Position > 0 — EntityStateMessage is sent.
        // Observable side-effect: SerializeClient calls ClearAllDirtyBits() after reliable
        // serialization, so IsDirty() returns false once the send path has been taken.
        [Test]
        public void BroadcastToServer_ReliableDirtyComponent_ClearsDirtyBitsAfterSend()
        {
            CreateNetworked(out _, out NetworkIdentity identity, out BroadcastTestBehaviour comp);
            comp.syncDirection = SyncDirection.ClientToServer;
            comp.syncMethod    = SyncMethod.Reliable;
            identity.isOwned   = true;
            NetworkClient.connection.owned.Add(identity);

            comp.SetSyncVarDirtyBit(1);
            Assert.That(comp.IsDirty(), Is.True);

            NetworkClient.NetworkLateUpdate();

            // ClearAllDirtyBits() is called inside SerializeClient when the reliable
            // component is serialized, proving writerReliable had data and
            // EntityStateMessage was dispatched.
            Assert.That(comp.IsDirty(), Is.False);
        }

        // Branch: writerUnreliableDelta.Position > 0 — EntityStateMessageUnreliableDelta is sent.
        // Also covers the !unreliableBaselineElapsed branch: lastUnreliableBaselineSent stays 0.
        // Observable side-effect: SerializeClient sets comp.lastSyncTime = NetworkTime.localTime
        // inside the delta path; a sentinel of -1.0 guarantees we detect the update even when
        // NetworkTime.localTime is 0.
        [Test]
        public void BroadcastToServer_HybridDirtyComponent_UpdatesLastSyncTimeAfterDeltaSend()
        {
            CreateNetworked(out _, out NetworkIdentity identity, out BroadcastTestBehaviour comp);
            comp.syncDirection = SyncDirection.ClientToServer;
            comp.syncMethod    = SyncMethod.Hybrid;
            identity.isOwned   = true;
            NetworkClient.connection.owned.Add(identity);

            // Sentinel distinguishes "updated" from any possible real value, even 0.
            comp.lastSyncTime = -1.0;
            comp.SetSyncVarDirtyBit(1);
            Assert.That(comp.IsDirty(), Is.True);

            PreventBaselineElapsed();
            NetworkClient.NetworkLateUpdate();

            // lastSyncTime assigned inside the SerializeClient delta path, confirming
            // writerUnreliableDelta had data and EntityStateMessageUnreliableDelta was sent.
            Assert.That(comp.lastSyncTime, Is.Not.EqualTo(-1.0));

            // Baseline interval has not elapsed, so lastUnreliableBaselineSent must be unchanged.
            Assert.That(identity.lastUnreliableBaselineSent, Is.EqualTo(0));
        }

        // Branch: unreliableBaselineElapsed && writerUnreliableBaseline.Position > 0
        //         — EntityStateMessageUnreliableBaseline is sent.
        // Observable side-effect: BroadcastToServer assigns
        // identity.lastUnreliableBaselineSent = (byte)Time.frameCount directly,
        // making this the most direct assertion for this branch.
        [Test]
        public void BroadcastToServer_BaselineElapsed_UpdatesLastUnreliableBaselineSent()
        {
            CreateNetworked(out _, out NetworkIdentity identity, out BroadcastTestBehaviour comp);
            comp.syncDirection = SyncDirection.ClientToServer;
            comp.syncMethod    = SyncMethod.Hybrid;
            identity.isOwned   = true;
            NetworkClient.connection.owned.Add(identity);

            comp.SetSyncVarDirtyBit(1);
            ForceBaselineElapsed();

            NetworkClient.NetworkLateUpdate();

            // lastUnreliableBaselineSent is set to (byte)Time.frameCount in BroadcastToServer
            // only when unreliableBaselineElapsed is true and the baseline writer has data.
            Assert.That(identity.lastUnreliableBaselineSent, Is.EqualTo((byte)Time.frameCount));
        }
    }
}