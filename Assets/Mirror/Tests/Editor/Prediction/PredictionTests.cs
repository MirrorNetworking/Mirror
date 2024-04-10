using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class PredictionTests
    {
        struct TestState : PredictedState
        {
            public double timestamp { get; set; }

            public Vector3 position { get; set; }
            public Vector3 positionDelta { get; set; }

            public Quaternion rotation { get; set; }
            public Quaternion rotationDelta { get; set; }

            public Vector3 velocity { get; set; }
            public Vector3 velocityDelta { get; set; }

            public Vector3 angularVelocity { get; set; }
            public Vector3 angularVelocityDelta { get; set; }

            public TestState(double timestamp, Vector3 position, Vector3 positionDelta, Vector3 velocity, Vector3 velocityDelta, Vector3 angularVelocity, Vector3 angularVelocityDelta)
            {
                this.timestamp = timestamp;
                this.position = position;
                this.positionDelta = positionDelta;
                this.rotation = Quaternion.identity;
                this.rotationDelta = Quaternion.identity;
                this.velocity = velocity;
                this.velocityDelta = velocityDelta;
                this.angularVelocity = angularVelocity;
                this.angularVelocityDelta = angularVelocityDelta;
            }
        }

        [Test]
        public void Sample_Empty()
        {
            // create an empty history
            SortedList<double, int> history = new SortedList<double, int>();

            // sample at 0.5. should fail because empty.
            Assert.That(Prediction.Sample(history, 0.5, out int before, out int after, out int afterIndex, out double t), Is.False);
        }

        [Test]
        public void Sample_OneEntry()
        {
            // create a history with 1 entry
            SortedList<double, int> history = new SortedList<double, int>();
            history.Add(0, 0);

            // sample at 0.5. should fail because sampling always needs to return the _two_ closest ones.
            Assert.That(Prediction.Sample(history, 0.5, out int before, out int after, out int afterIndex, out double t), Is.False);
        }

        [Test]
        public void Sample_BeforeOldest()
        {
            // create a history with 3 entries
            SortedList<double, int> history = new SortedList<double, int>();
            history.Add(0, 0);
            history.Add(1, 1);
            history.Add(2, 2);

            // sample at -1 before the oldest. should fail since there wouldn't be two closest entries around it.
            Assert.That(Prediction.Sample(history, -1, out int before, out int after, out int afterIndex, out double t), Is.False);
        }

        [Test]
        public void Sample_AfterNewest()
        {
            // create a history with 3 entries
            SortedList<double, int> history = new SortedList<double, int>();
            history.Add(0, 0);
            history.Add(1, 1);
            history.Add(2, 2);

            // sample at 3 after the newest. should fail since there wouldn't be two closest entries around it.
            Assert.That(Prediction.Sample(history, 3, out int before, out int after, out int afterIndex, out double t), Is.False);
        }

        [Test]
        public void Sample_MultipleEntries()
        {
            // create a history with 3 entries
            SortedList<double, int> history = new SortedList<double, int>();
            history.Add(0, 0);
            history.Add(1, 1);
            history.Add(2, 2);

            // sample at exactly 0.0: should return '0' twice since it's exactly on that data point.
            Assert.That(Prediction.Sample(history, 0.0, out int before, out int after, out int afterIndex, out double t), Is.True);
            Assert.That(before, Is.EqualTo(0));
            Assert.That(after, Is.EqualTo(0));
            Assert.That(afterIndex, Is.EqualTo(0));
            Assert.That(t, Is.EqualTo(0.0));

            // sample at 0.5
            Assert.That(Prediction.Sample(history, 0.5, out before, out after, out afterIndex, out t), Is.True);
            Assert.That(before, Is.EqualTo(0));
            Assert.That(after, Is.EqualTo(1));
            Assert.That(afterIndex, Is.EqualTo(1));
            Assert.That(t, Is.EqualTo(0.5));

            // sample at 1.5
            Assert.That(Prediction.Sample(history, 1.5, out before, out after, out afterIndex, out t), Is.True);
            Assert.That(before, Is.EqualTo(1));
            Assert.That(after, Is.EqualTo(2));
            Assert.That(afterIndex, Is.EqualTo(2));
            Assert.That(t, Is.EqualTo(0.5));

            // sample at exactly 2.0. should return '2' twice since it's exactly on that data point.
            Assert.That(Prediction.Sample(history, 2.0, out before, out after, out afterIndex, out t), Is.True);
            Assert.That(before, Is.EqualTo(2));
            Assert.That(after, Is.EqualTo(2));
            Assert.That(afterIndex, Is.EqualTo(2));
            Assert.That(t, Is.EqualTo(0.0));
        }

        ////////////////////////////////////////////////////////////////////////
        [Test]
        public void CorrectHistory()
        {
            // prepare a straight forward history
            SortedList<double, TestState> history = new SortedList<double, TestState>();

            // (0,0,0) with delta (0,0,0) from previous:
            history.Add(0, new TestState(0,   new Vector3(0, 0, 0),    new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 0)));

            // (1,0,0) with delta (1,0,0) from previous:
            history.Add(1, new TestState(1,   new Vector3(1, 0, 0),    new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0)));

            // (2,0,0) with delta (1,0,0) from previous:
            history.Add(2, new TestState(2,   new Vector3(2, 0, 0),    new Vector3(1, 0, 0), new Vector3(2, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0), new Vector3(1, 0, 0)));

            // (3,0,0) with delta (1,0,0) from previous:
            history.Add(3, new TestState(3,   new Vector3(3, 0, 0),    new Vector3(1, 0, 0), new Vector3(3, 0, 0), new Vector3(1, 0, 0), new Vector3(3, 0, 0), new Vector3(1, 0, 0)));

            // client receives a correction from server between t=1 and t=2.
            // exactly t=1.5 where position should be 1.5, server says it's +0.1 = 1.6
            // deltas are zero because that's how PredictedBody.Serialize sends them, alwasy at zero.
            TestState correction = new TestState(1.5, new Vector3(1.6f, 0, 0), Vector3.zero, new Vector3(1.6f, 0, 0), Vector3.zero, new Vector3(1.6f, 0, 0), Vector3.zero);

            // Sample() will find that the value before correction is at  t=1 and after at t=2.
            Assert.That(Prediction.Sample(history, correction.timestamp, out TestState before, out TestState after, out int afterIndex, out double t), Is.True);
            Assert.That(before.timestamp, Is.EqualTo(1));
            Assert.That(after.timestamp, Is.EqualTo(2));
            Assert.That(afterIndex, Is.EqualTo(2));
            Assert.That(t, Is.EqualTo(0.5));

            // ... this is where we would interpolate (before, after, 0.5) and
            //     compare to decide if we need to correct.
            //     assume we decided that a correction is necessary ...

            // correct history with the received server state
            const int historyLimit = 32;
            Prediction.CorrectHistory(history, historyLimit, correction, before, after, afterIndex);

            // PERFORMANCE OPTIMIZATION: nothing is inserted anymore, values are only adjusted.
            //   there should be 4 initial + 1 corrected = 5 entries now
            //   Assert.That(history.Count, Is.EqualTo(5));
            Assert.That(history.Count, Is.EqualTo(4));

            // first entry at t=0 should be unchanged, since we corrected after that one.
            Assert.That(history.Keys[0], Is.EqualTo(0));
            Assert.That(history.Values[0].position.x, Is.EqualTo(0));
            Assert.That(history.Values[0].positionDelta.x, Is.EqualTo(0));
            Assert.That(history.Values[0].velocity.x, Is.EqualTo(0));
            Assert.That(history.Values[0].velocityDelta.x, Is.EqualTo(0));
            Assert.That(history.Values[0].angularVelocity.x, Is.EqualTo(0));
            Assert.That(history.Values[0].angularVelocityDelta.x, Is.EqualTo(0));

            // second entry at t=1 should be unchanged, since we corrected after that one.
            Assert.That(history.Keys[1], Is.EqualTo(1));
            Assert.That(history.Values[1].position.x, Is.EqualTo(1));
            Assert.That(history.Values[1].positionDelta.x, Is.EqualTo(1));
            Assert.That(history.Values[1].velocity.x, Is.EqualTo(1));
            Assert.That(history.Values[1].velocityDelta.x, Is.EqualTo(1));
            Assert.That(history.Values[1].angularVelocity.x, Is.EqualTo(1));
            Assert.That(history.Values[1].angularVelocityDelta.x, Is.EqualTo(1));

            // PERFORMANCE OPTIMIZATION: nothing is inserted anymore, values are only adjusted.
            //   third entry at t=1.5 should be the received state.
            //   absolute values should be the correction, without any deltas since
            //   server doesn't send those and we don't need them.
            //   Assert.That(history.Keys[2], Is.EqualTo(1.5));
            //   Assert.That(history.Values[2].position.x, Is.EqualTo(1.6f).Within(0.001f));
            //   Assert.That(history.Values[2].positionDelta.x, Is.EqualTo(0));
            //   Assert.That(history.Values[2].velocity.x, Is.EqualTo(1.6f).Within(0.001f));
            //   Assert.That(history.Values[2].velocityDelta.x, Is.EqualTo(0));
            //   Assert.That(history.Values[2].angularVelocity.x, Is.EqualTo(1.6f).Within(0.001f));
            //   Assert.That(history.Values[2].angularVelocityDelta.x, Is.EqualTo(0));

            // fourth entry at t=2:
            // delta was from t=1.0 @ 1 to t=2.0 @ 2 = 1.0
            // we inserted at t=1.5 which is half way between t=1 and t=2.
            // the delta at t=1.5 would've been 0.5.
            // => the inserted position is at t=1.6
            // => add the relative delta of 0.5 = 2.1
            Assert.That(history.Keys[2], Is.EqualTo(2.0));
            Assert.That(history.Values[2].position.x, Is.EqualTo(2.1).Within(0.001f));
            Assert.That(history.Values[2].positionDelta.x, Is.EqualTo(0.5).Within(0.001f));
            Assert.That(history.Values[2].velocity.x, Is.EqualTo(2.1).Within(0.001f));
            Assert.That(history.Values[2].velocityDelta.x, Is.EqualTo(0.5).Within(0.001f));
            Assert.That(history.Values[2].angularVelocity.x, Is.EqualTo(2.1).Within(0.001f));
            Assert.That(history.Values[2].angularVelocityDelta.x, Is.EqualTo(0.5));

            // fifth entry at t=3:
            // client moved by a delta of 1 here, and that remains unchanged.
            // absolute position was 3.0 but if we apply the delta of 1 to the one before at 2.1,
            // we get the new position of 3.1
            Assert.That(history.Keys[3], Is.EqualTo(3.0));
            Assert.That(history.Values[3].position.x, Is.EqualTo(3.1).Within(0.001f));
            Assert.That(history.Values[3].positionDelta.x, Is.EqualTo(1.0).Within(0.001f));
            Assert.That(history.Values[3].velocity.x, Is.EqualTo(3.1).Within(0.001f));
            Assert.That(history.Values[3].velocityDelta.x, Is.EqualTo(1.0).Within(0.001f));
            Assert.That(history.Values[3].angularVelocity.x, Is.EqualTo(3.1).Within(0.001f));
            Assert.That(history.Values[3].angularVelocityDelta.x, Is.EqualTo(1.0).Within(0.001f));
        }
    }
}
