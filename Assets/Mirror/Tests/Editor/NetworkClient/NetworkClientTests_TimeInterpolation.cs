using NUnit.Framework;

namespace Mirror.Tests.NetworkClients
{
    /// <summary>
    /// Tests for the snapshot-interpolation subsystem in NetworkClient_TimeInterpolation.cs.
    /// Covers InitTimeInterpolation, OnTimeSnapshot, UpdateTimeInterpolation, computed
    /// properties, and the disconnect-reset path.
    /// </summary>
    public class NetworkClientTests_TimeInterpolation : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // sendRate / sendInterval are driven by NetworkServer, so a listening
            // server must exist before we can meaningfully test bufferTime etc.
            NetworkServer.Listen(1);
            // Reset snapshot settings to defaults so tests cannot pollute each other.
            NetworkClient.snapshotSettings = new SnapshotInterpolationSettings();
            // ConnectHost runs Initialize() -> InitTimeInterpolation(), which
            // allocates the EMA objects required by OnTimeSnapshot.
            NetworkClient.ConnectHost();
        }

        // ── InitTimeInterpolation (via ConnectHost / Initialize) ──────────────

        [Test]
        public void InitTimeInterpolation_SnapshotsAreEmpty()
        {
            Assert.That(NetworkClient.snapshots.Count, Is.EqualTo(0));
        }

        [Test]
        public void InitTimeInterpolation_LocalTimelineIsZero()
        {
            Assert.That(NetworkClient.localTimeline, Is.EqualTo(0));
        }

        [Test]
        public void InitTimeInterpolation_LocalTimescaleIsOne()
        {
            Assert.That(NetworkClient.localTimescale, Is.EqualTo(1));
        }

        [Test]
        public void InitTimeInterpolation_SetsBufferTimeMultiplierFromSettings()
        {
            // bufferTimeMultiplier is copied from snapshotSettings during init.
            Assert.That(NetworkClient.bufferTimeMultiplier,
                Is.EqualTo(NetworkClient.snapshotSettings.bufferTimeMultiplier));
        }

        // ── Computed properties ───────────────────────────────────────────────

        [Test]
        public void BufferTime_EqualsProductOfSendIntervalAndCurrentMultiplier()
        {
            NetworkClient.bufferTimeMultiplier = 4.0;
            double expected = NetworkServer.sendInterval * 4.0;
            Assert.That(NetworkClient.bufferTime, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void InitialBufferTime_EqualsProductOfSendIntervalAndSettingsMultiplier()
        {
            double expected = NetworkServer.sendInterval * NetworkClient.snapshotSettings.bufferTimeMultiplier;
            Assert.That(NetworkClient.initialBufferTime, Is.EqualTo(expected).Within(1e-9));
        }

        // ── OnTimeSnapshot ────────────────────────────────────────────────────

        [Test]
        public void OnTimeSnapshot_AddsSnapshotToBuffer()
        {
            Assert.That(NetworkClient.snapshots.Count, Is.EqualTo(0));
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(1.0, 0.0));
            Assert.That(NetworkClient.snapshots.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnTimeSnapshot_InitializesLocalTimelineOnFirstCall()
        {
            // InsertAndAdjust sets localTimeline = remoteTime - bufferTime
            // when the buffer is empty (first snapshot).
            Assert.That(NetworkClient.localTimeline, Is.EqualTo(0));
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(1.0, 0.0));
            // With remoteTime=1.0 and bufferTime = sendInterval*2 ≈ 0.067,
            // localTimeline ≈ 0.933 — definitely not 0.
            Assert.That(NetworkClient.localTimeline, Is.Not.EqualTo(0));
        }

        [Test]
        public void OnTimeSnapshot_MultipleDistinctTimestampsGrowBuffer()
        {
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(1.0, 0.000));
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(2.0, 0.033));
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(3.0, 0.066));
            Assert.That(NetworkClient.snapshots.Count, Is.EqualTo(3));
        }

        [Test]
        public void OnTimeSnapshot_DuplicateRemoteTimeDoesNotGrowBuffer()
        {
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(1.0, 0.0));
            int countAfterFirst = NetworkClient.snapshots.Count;
            // InsertIfNotExists returns false for duplicate keys; count must not increase.
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(1.0, 0.1));
            Assert.That(NetworkClient.snapshots.Count, Is.EqualTo(countAfterFirst));
        }

        [Test]
        public void OnTimeSnapshot_WithDynamicAdjustment_RecalculatesBufferTimeMultiplier()
        {
            NetworkClient.snapshotSettings.dynamicAdjustment = true;
            // Set an obviously wrong value; the dynamic path must overwrite it.
            NetworkClient.bufferTimeMultiplier = 99.0;
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(1.0, 0.0));
            Assert.That(NetworkClient.bufferTimeMultiplier, Is.Not.EqualTo(99.0));
        }

        [Test]
        public void OnTimeSnapshot_WithoutDynamicAdjustment_PreservesBufferTimeMultiplier()
        {
            NetworkClient.snapshotSettings.dynamicAdjustment = false;
            NetworkClient.bufferTimeMultiplier = 99.0;
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(1.0, 0.0));
            // Static path: the if-block is skipped, so our value is preserved.
            Assert.That(NetworkClient.bufferTimeMultiplier, Is.EqualTo(99.0));
        }

        // ── UpdateTimeInterpolation (via NetworkEarlyUpdate) ──────────────────

        [Test]
        public void UpdateTimeInterpolation_DoesNotThrowWhenBufferEmpty()
        {
            // The early-out guard (if snapshots.Count > 0) must prevent any
            // NRE or IndexOutOfRange when there are no snapshots at all.
            Assert.DoesNotThrow(() => NetworkClient.NetworkEarlyUpdate());
        }

        [Test]
        public void UpdateTimeInterpolation_DoesNotThrowWithSnapshots()
        {
            // Two snapshots ensure StepInterpolation has a valid pair to sample.
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(1.0, 0.000));
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(2.0, 0.033));
            Assert.DoesNotThrow(() => NetworkClient.NetworkEarlyUpdate());
        }

        // ── Disconnect resets interpolation state ────────────────────────────────────

        [Test]
        public void Disconnect_ClearsSnapshotBuffer()
        {
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(1.0, 0.0));
            Assert.That(NetworkClient.snapshots.Count, Is.GreaterThan(0));

            NetworkClient.Disconnect();
            UpdateTransport();

            Assert.That(NetworkClient.snapshots.Count, Is.EqualTo(0));
        }

        [Test]
        public void Disconnect_ResetsLocalTimeline()
        {
            NetworkClient.OnTimeSnapshot(new TimeSnapshot(1.0, 0.0));
            // localTimeline was initialised to a non-zero value by InsertAndAdjust.
            Assert.That(NetworkClient.localTimeline, Is.Not.EqualTo(0));

            NetworkClient.Disconnect();
            UpdateTransport();

            Assert.That(NetworkClient.localTimeline, Is.EqualTo(0));
        }
    }
}