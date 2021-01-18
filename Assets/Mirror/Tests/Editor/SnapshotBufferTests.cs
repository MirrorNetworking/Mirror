using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RangeAttribute = NUnit.Framework.RangeAttribute;

namespace Mirror.TransformSyncing.Tests
{
    public class SnapshotBufferTests
    {
        public class IsEmpty
        {
            [Test]
            public void ShouldBeTrueOnNewBuffer()
            {
                SnapshotBuffer buffer = new SnapshotBuffer();
                Assert.That(buffer.IsEmpty, Is.True);
            }

            [Test]
            public void ShouldBeFalseAfterAddingToBuffer()
            {
                SnapshotBuffer buffer = new SnapshotBuffer();
                buffer.AddSnapShot(default, default);
                Assert.That(buffer.IsEmpty, Is.False);
            }

            [Test]
            public void ShouldBeTrueAfterRemovingFromBuffer()
            {
                SnapshotBuffer buffer = new SnapshotBuffer();
                buffer.AddSnapShot(default, 0);
                buffer.RemoveOldSnapshots(1);
                Assert.That(buffer.IsEmpty, Is.True);
            }
        }

        public class RemoveSnapshots
        {
            [Test]
            public void ShouldOnlyRemoveOld()
            {
                SnapshotBuffer buffer = new SnapshotBuffer();
                buffer.AddSnapShot(default, 0);
                buffer.AddSnapShot(default, 1);
                Assert.That(buffer.SnapshotCount, Is.EqualTo(2));

                buffer.RemoveOldSnapshots(0.5f);
                Assert.That(buffer.SnapshotCount, Is.EqualTo(1));

                buffer.RemoveOldSnapshots(1.5f);
                Assert.That(buffer.SnapshotCount, Is.EqualTo(0));
            }
        }

        public class AddSnapshot
        {
            [Test]
            public void ShouldIncreaseCount([Range(1, 5)] int count)
            {
                SnapshotBuffer buffer = new SnapshotBuffer();
                for (int i = 0; i < count; i++)
                {
                    Assert.That(buffer.SnapshotCount, Is.EqualTo(i));
                    buffer.AddSnapShot(default, i);
                    Assert.That(buffer.SnapshotCount, Is.EqualTo(i + 1));
                }
            }

            [Test]
            public void ShouldGiveErrorIfOutOfOrder()
            {
                const float time1 = 1.1f;
                const float time2 = 0.9f;

                SnapshotBuffer buffer = new SnapshotBuffer();
                buffer.AddSnapShot(default, time1);
                LogAssert.Expect(LogType.Error, $"Adding Snapshot to buffer out of order, last t={time1:0.000}, new t={time2:0.000}");
                buffer.AddSnapShot(default, time2);
            }
        }

        public class GetInterpolation
        {
            private ILogger logger;
            private LogType oldFilterLogType;

            // set and reset log level so tests can validate
            [SetUp]
            public void SetUp()
            {
                logger = LogFactory.GetLogger<SnapshotBuffer>(LogType.Error);
                oldFilterLogType = logger.filterLogType;
                logger.filterLogType = LogType.Log;
            }
            [TearDown]
            public void TearDown()
            {
                logger.filterLogType = oldFilterLogType;
            }

            [Test]
            public void ShouldGiveErrorWhenEmpty()
            {
                SnapshotBuffer buffer = new SnapshotBuffer();

                LogAssert.Expect(LogType.Error, "No snapshots, returning default");
                TransformState value = buffer.GetLinearInterpolation(default);
                Assert.That(value, Is.EqualTo(default(TransformState)));
            }

            [Test]
            public void ShouldReturnFirstIfOnly1Snapshot([Range(0, 10, 1f)] float now)
            {
                TransformState state = new TransformState(Vector3.one, Quaternion.identity);
                SnapshotBuffer buffer = new SnapshotBuffer();
                buffer.AddSnapShot(state, default);

                LogAssert.Expect(LogType.Log, "First snapshot");
                TransformState value = buffer.GetLinearInterpolation(now);
                Assert.That(value, Is.EqualTo(state));
            }

            [Test]
            public void ShouldReturnFirstIfNowIsBeforeFirst([Range(0, 1, 0.1f)] float now)
            {
                const float time1 = 1.1f;
                const float time2 = 1.5f;
                TransformState state1 = new TransformState(Vector3.one, Quaternion.identity);
                TransformState state2 = new TransformState(Vector3.one * 2, Quaternion.identity);

                SnapshotBuffer buffer = new SnapshotBuffer();
                buffer.AddSnapShot(state1, time1);
                buffer.AddSnapShot(state2, time2);

                LogAssert.Expect(LogType.Log, $"No snapshots for t={now:0.000}, using earliest t={time1:0.000}");
                TransformState value = buffer.GetLinearInterpolation(now);
                Assert.That(value, Is.EqualTo(state1));
            }

            [Test]
            public void ShouldReturnLastIfNowIsAfterLast([Range(1.6f, 2.6f, 0.1f)] float now)
            {
                const float time1 = 1.1f;
                const float time2 = 1.5f;
                TransformState state1 = new TransformState(Vector3.one, Quaternion.identity);
                TransformState state2 = new TransformState(Vector3.one * 2, Quaternion.identity);

                SnapshotBuffer buffer = new SnapshotBuffer();
                buffer.AddSnapShot(state1, time1);
                buffer.AddSnapShot(state2, time2);

                LogAssert.Expect(LogType.Warning, $"No snapshots for t={now:0.000}, using first t={time1:0.000} last t={time2:0.000}");
                TransformState value = buffer.GetLinearInterpolation(now);
                Assert.That(value, Is.EqualTo(state2));
            }

            [Test]
            public void ShouldReturnInterpolationForNowBetween2Snapshots([Range(1f, 2f, 0.1f)] float now)
            {
                const float time1 = 1f;
                const float time2 = 2f;
                TransformState state1 = new TransformState(Vector3.one, Quaternion.identity);
                TransformState state2 = new TransformState(Vector3.one * 2, Quaternion.identity);
                TransformState expected = new TransformState(Vector3.Lerp(state1.position, state2.position, now - time1), Quaternion.identity);

                SnapshotBuffer buffer = new SnapshotBuffer();
                buffer.AddSnapShot(state1, time1);
                buffer.AddSnapShot(state2, time2);

                TransformState value = buffer.GetLinearInterpolation(now);
                Assert.That(value, Is.EqualTo(expected));
            }
        }

        public class DebugToString
        {
            [Test]
            public void ShouldSayEmptyBuffer()
            {
                SnapshotBuffer buffer = new SnapshotBuffer();
                string str = buffer.ToString();
                Assert.That(str, Is.EqualTo("Buffer Empty"));
            }
            [Test]
            public void ShouldListSnapshotsInBuffer()
            {
                SnapshotBuffer buffer = new SnapshotBuffer();
                string str = buffer.ToString();
                Assert.Ignore("NotImplemented");
            }
        }
    }
}
