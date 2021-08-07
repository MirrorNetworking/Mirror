
using System;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Tests
{
    // a simple snapshot with timestamp & interpolation
    struct SimpleSnapshot : Snapshot
    {
        public double remoteTimestamp { get; set; }
        public double localTimestamp { get; set; }
        public double value;

        public SimpleSnapshot(double remoteTimestamp, double localTimestamp, double value)
        {
            this.remoteTimestamp = remoteTimestamp;
            this.localTimestamp = localTimestamp;
            this.value = value;
        }

        public static SimpleSnapshot Interpolate(SimpleSnapshot from, SimpleSnapshot to, double t) =>
            new SimpleSnapshot(
                // interpolated snapshot is applied directly. don't need timestamps.
                0, 0,
                // lerp unclamped in case we ever need to extrapolate.
                // atm SnapshotInterpolation never does.
                Mathd.LerpUnclamped(from.value, to.value, t));
    }

    public class SnapshotInterpolationTests
    {
        // buffer for convenience so we don't have to create it manually each time
        SortedList<double, SimpleSnapshot> buffer;

        [SetUp]
        public void SetUp()
        {
            buffer = new SortedList<double, SimpleSnapshot>();
        }

        [Test]
        public void InsertIfNewEnough()
        {
            // inserting a first value should always work
            SimpleSnapshot first = new SimpleSnapshot(1, 1, 0);
            SnapshotInterpolation.InsertIfNewEnough(first, buffer);
            Assert.That(buffer.Count, Is.EqualTo(1));

            // insert before first should not work
            SimpleSnapshot before = new SimpleSnapshot(0.5, 0.5, 0);
            SnapshotInterpolation.InsertIfNewEnough(before, buffer);
            Assert.That(buffer.Count, Is.EqualTo(1));

            // insert after first should work
            SimpleSnapshot second = new SimpleSnapshot(2, 2, 0);
            SnapshotInterpolation.InsertIfNewEnough(second, buffer);
            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer.Values[0], Is.EqualTo(first));
            Assert.That(buffer.Values[1], Is.EqualTo(second));

            // insert after second should work
            SimpleSnapshot after = new SimpleSnapshot(2.5, 2.5, 0);
            SnapshotInterpolation.InsertIfNewEnough(after, buffer);
            Assert.That(buffer.Count, Is.EqualTo(3));
            Assert.That(buffer.Values[0], Is.EqualTo(first));
            Assert.That(buffer.Values[1], Is.EqualTo(second));
            Assert.That(buffer.Values[2], Is.EqualTo(after));
        }

        // the 'ACB' problem:
        //   if we have a snapshot A at t=0 and C at t=2,
        //   we start interpolating between them.
        //   if suddenly B at t=1 comes in unexpectely,
        //   we should NOT suddenly steer towards B.
        // => inserting between the first two snapshot should never be allowed
        //    in order to avoid all kinds of edge cases.
        [Test]
        public void InsertIfNewEnough_ACB_Problem()
        {
            SimpleSnapshot a = new SimpleSnapshot(0, 0, 0);
            SimpleSnapshot b = new SimpleSnapshot(1, 1, 0);
            SimpleSnapshot c = new SimpleSnapshot(2, 2, 0);

            // insert A and C
            SnapshotInterpolation.InsertIfNewEnough(a, buffer);
            SnapshotInterpolation.InsertIfNewEnough(c, buffer);

            // trying to insert B between the first two snapshots should fail
            SnapshotInterpolation.InsertIfNewEnough(b, buffer);
            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer.Values[0], Is.EqualTo(a));
            Assert.That(buffer.Values[1], Is.EqualTo(c));
        }

        // the 'first is lagging' problem:
        //   server sends A, B.
        //   A is lagging behind by 2000ms for whatever reason.
        //   we get B first.
        //   B should remain the first snapshot, the lagging A should be dropped
        [Test]
        public void InsertIfNewEnough_FirstIsLagging_Problem()
        {
            SimpleSnapshot a = new SimpleSnapshot(0, 0, 0);
            SimpleSnapshot b = new SimpleSnapshot(1, 1, 0);

            // insert B. A is still delayed.
            SnapshotInterpolation.InsertIfNewEnough(b, buffer);

            // now the delayed A comes in.
            // timestamp is before B though.
            // but it should still be dropped.
            SnapshotInterpolation.InsertIfNewEnough(a, buffer);
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.Values[0], Is.EqualTo(b));
        }

        [Test]
        public void HasAmountOlderThan_NotEnough()
        {
            // only add two
            SimpleSnapshot a = new SimpleSnapshot(0, 0, 0);
            SimpleSnapshot b = new SimpleSnapshot(1, 1, 0);
            buffer.Add(a.remoteTimestamp, a);
            buffer.Add(b.remoteTimestamp, b);

            // shouldn't have more old enough than two
            // because we don't have more than two
            Assert.That(SnapshotInterpolation.HasAmountOlderThan(buffer, 0, 3), Is.False);
        }

        [Test]
        public void HasAmountOlderThan_EnoughButNotOldEnough()
        {
            // add three
            SimpleSnapshot a = new SimpleSnapshot(0, 0, 0);
            SimpleSnapshot b = new SimpleSnapshot(1, 1, 0);
            SimpleSnapshot c = new SimpleSnapshot(2, 2, 0);
            buffer.Add(a.remoteTimestamp, a);
            buffer.Add(b.remoteTimestamp, b);
            buffer.Add(c.remoteTimestamp, c);

            // check at time = 1.9, where third one would not be old enough.
            Assert.That(SnapshotInterpolation.HasAmountOlderThan(buffer, 1.9, 3), Is.False);
        }

        [Test]
        public void HasAmountOlderThan_EnoughAndOldEnough()
        {
            // add three
            SimpleSnapshot a = new SimpleSnapshot(0, 0, 0);
            SimpleSnapshot b = new SimpleSnapshot(1, 1, 0);
            SimpleSnapshot c = new SimpleSnapshot(2, 2, 0);
            buffer.Add(a.remoteTimestamp, a);
            buffer.Add(b.remoteTimestamp, b);
            buffer.Add(c.remoteTimestamp, c);

            // check at time = 2.1, where third one would be old enough.
            Assert.That(SnapshotInterpolation.HasAmountOlderThan(buffer, 2.1, 3), Is.True);
        }

        // UDP messages might arrive twice sometimes.
        // make sure InsertIfNewEnough can handle it.
        [Test]
        public void InsertIfNewEnough_Duplicate()
        {
            SimpleSnapshot a = new SimpleSnapshot(0, 0, 0);
            SimpleSnapshot b = new SimpleSnapshot(1, 1, 0);
            SimpleSnapshot c = new SimpleSnapshot(2, 2, 0);

            // add two valid snapshots first.
            // we can't add 'duplicates' before 3rd and 4th anyway.
            SnapshotInterpolation.InsertIfNewEnough(a, buffer);
            SnapshotInterpolation.InsertIfNewEnough(b, buffer);

            // insert C which is newer than B.
            // then insert it again because it arrive twice.
            SnapshotInterpolation.InsertIfNewEnough(c, buffer);
            SnapshotInterpolation.InsertIfNewEnough(c, buffer);

            // count should still be 3.
            Assert.That(buffer.Count, Is.EqualTo(3));
        }

        [Test]
        public void CalculateCatchup_Empty()
        {
            // make sure nothing happens with buffer size = 0
            Assert.That(SnapshotInterpolation.CalculateCatchup(buffer, 0, 10), Is.EqualTo(0));
        }

        [Test]
        public void CalculateCatchup_None()
        {
            // add one
            buffer.Add(0, default);

            // catch-up starts at threshold = 1. so nothing.
            Assert.That(SnapshotInterpolation.CalculateCatchup(buffer, 1, 10), Is.EqualTo(0));
        }

        [Test]
        public void GetFirstSecondAndDelta()
        {
            // add three
            SimpleSnapshot a = new SimpleSnapshot(0, 1, 0);
            SimpleSnapshot b = new SimpleSnapshot(2, 3, 0);
            SimpleSnapshot c = new SimpleSnapshot(10, 20, 0);
            buffer.Add(a.remoteTimestamp, a);
            buffer.Add(b.remoteTimestamp, b);
            buffer.Add(c.remoteTimestamp, c);

            SnapshotInterpolation.GetFirstSecondAndDelta(buffer, out SimpleSnapshot first, out SimpleSnapshot second, out double delta);
            Assert.That(first, Is.EqualTo(a));
            Assert.That(second, Is.EqualTo(b));
            Assert.That(delta, Is.EqualTo(b.remoteTimestamp - a.remoteTimestamp));
        }

        [Test]
        public void CalculateCatchup_Multiple()
        {
            // add three
            buffer.Add(0, default);
            buffer.Add(1, default);
            buffer.Add(2, default);

            // catch-up starts at threshold = 1. so two are multiplied by 10.
            Assert.That(SnapshotInterpolation.CalculateCatchup(buffer, 1, 10), Is.EqualTo(20));
        }

        // first step: with empty buffer and defaults, nothing should happen
        [Test]
        public void Compute_Step1_DefaultDoesNothing()
        {
            // compute with defaults
            double localTime = 0;
            double deltaTime = 0;
            double interpolationTime = 0;
            float bufferTime = 0;
            int catchupThreshold = Int32.MaxValue;
            float catchupMultiplier = 0;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should not spit out any snapshot to apply
            Assert.That(result, Is.False);
            // no interpolation should have happened yet
            Assert.That(interpolationTime, Is.EqualTo(0));
            // buffer should still be untouched
            Assert.That(buffer.Count, Is.EqualTo(0));
        }

        // third step: compute should always wait until the first two snapshots
        //             are older than the time we buffer ('bufferTime')
        //             => test for both snapshots not old enough
        [Test]
        public void Compute_Step3_WaitsUntilBufferTime()
        {
            // add two snapshots that are barely not old enough
            // (localTime - bufferTime)
            // IMPORTANT: use a 'definitely old enough' remoteTime to make sure
            //            that compute() actually checks LOCAL, not REMOTE time!
            SimpleSnapshot first = new SimpleSnapshot(0.1, 0.1, 0);
            SimpleSnapshot second = new SimpleSnapshot(0.9, 1.1, 0);
            buffer.Add(first.remoteTimestamp, first);
            buffer.Add(second.remoteTimestamp, second);

            // compute with initialized remoteTime and buffer time of 2 seconds
            // and a delta time to be sure that we move along it no matter what.
            double localTime = 3;
            double deltaTime = 0.5;
            double interpolationTime = 0;
            float bufferTime = 2;
            int catchupThreshold = Int32.MaxValue;
            float catchupMultiplier = 0;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should not spit out any snapshot to apply
            Assert.That(result, Is.False);
            // no interpolation should happen yet (not old enough)
            Assert.That(interpolationTime, Is.EqualTo(0));
            // buffer should be untouched
            Assert.That(buffer.Count, Is.EqualTo(2));
        }

        // third step: compute should always wait until the first two snapshots
        //             are older than the time we buffer ('bufferTime')
        //             => test for only one snapshot which is old enough
        [Test]
        public void Compute_Step3_WaitsForSecondSnapshot()
        {
            // add a snapshot at t = 0
            SimpleSnapshot first = new SimpleSnapshot(0, 0, 0);
            buffer.Add(first.remoteTimestamp, first);

            // compute at localTime = 2 with bufferTime = 1
            // so the threshold is anything < t=1
            double localTime = 2;
            double deltaTime = 0;
            double interpolationTime = 0;
            float bufferTime = 1;
            int catchupThreshold = Int32.MaxValue;
            float catchupMultiplier = 0;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should not spit out any snapshot to apply
            Assert.That(result, Is.False);
            // no interpolation should happen yet (not enough snapshots)
            Assert.That(interpolationTime, Is.EqualTo(0));
            // buffer should be untouched
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        // fourth step: compute should begin if we have two old enough snapshots
        [Test]
        public void Compute_Step4_InterpolateWithTwoOldEnoughSnapshots()
        {
            // add two old enough snapshots
            // (localTime - bufferTime)
            SimpleSnapshot first = new SimpleSnapshot(0, 0, 1);
            // IMPORTANT: second snapshot delta is != 1 so we can be sure that
            //            interpolationTime result is actual time, not 't' ratio.
            //            for a delta of 1, absolute and relative values would
            //            return the same results.
            SimpleSnapshot second = new SimpleSnapshot(2, 2, 2);
            buffer.Add(first.remoteTimestamp, first);
            buffer.Add(second.remoteTimestamp, second);

            // compute with initialized remoteTime and buffer time of 2 seconds
            // and a delta time to be sure that we move along it no matter what.
            double localTime = 4;
            double deltaTime = 1.5;
            double interpolationTime = 0;
            float bufferTime = 2;
            int catchupThreshold = Int32.MaxValue;
            float catchupMultiplier = 0;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should spit out the interpolated snapshot
            Assert.That(result, Is.True);
            // interpolation started just now, from 0.
            // and deltaTime is 1.5, so we should be at 1.5 now.
            Assert.That(interpolationTime, Is.EqualTo(1.5));
            // buffer should be untouched, we are still interpolating between the two
            Assert.That(buffer.Count, Is.EqualTo(2));
            // interpolationTime is at 1.5, so 3/4 between first & second.
            // computed snapshot should be interpolated at 3/4ths.
            Assert.That(computed.value, Is.EqualTo(1.75).Within(Mathf.Epsilon));
        }

        // fourth step: compute should begin if we have two old enough snapshots
        //              => test with 3 snapshots to make sure the third one
        //                 isn't touched while t between [0,1]
        [Test]
        public void Compute_Step4_InterpolateWithThreeOldEnoughSnapshots()
        {
            // add three old enough snapshots.
            // (localTime - bufferTime)
            SimpleSnapshot first = new SimpleSnapshot(0, 0, 1);
            SimpleSnapshot second = new SimpleSnapshot(1, 1, 2);
            SimpleSnapshot third = new SimpleSnapshot(2, 2, 2);
            buffer.Add(first.remoteTimestamp, first);
            buffer.Add(second.remoteTimestamp, second);
            buffer.Add(third.remoteTimestamp, third);

            // compute with initialized remoteTime and buffer time of 2 seconds
            // and a delta time to be sure that we move along it no matter what.
            double localTime = 4;
            double deltaTime = 0.5;
            double interpolationTime = 0;
            float bufferTime = 2;
            int catchupThreshold = Int32.MaxValue;
            float catchupMultiplier = 0;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should spit out the interpolated snapshot
            Assert.That(result, Is.True);
            // interpolation started just now, from 0.
            // and deltaTime is 0.5, so we should be at 0.5 now.
            Assert.That(interpolationTime, Is.EqualTo(0.5));
            // buffer should be untouched, we are still interpolating between
            // the first two. third should still be there.
            Assert.That(buffer.Count, Is.EqualTo(3));
            // computed snapshot should be interpolated in the middle
            Assert.That(computed.value, Is.EqualTo(1.5).Within(Mathf.Epsilon));
        }

        // fourth step: simulate interpolation after a long time of no updates.
        //              for example, a mobile user might put the app in the
        //              background for a minute.
        [Test]
        public void Compute_Step4_InterpolateAfterLongPause()
        {
            // add two immediate, and one that arrives 100s later
            // (localTime - bufferTime)
            SimpleSnapshot first = new SimpleSnapshot(0, 0, 0);
            SimpleSnapshot second = new SimpleSnapshot(1, 1, 1);
            SimpleSnapshot third = new SimpleSnapshot(101, 2, 101);
            buffer.Add(first.remoteTimestamp, first);
            buffer.Add(second.remoteTimestamp, second);
            buffer.Add(third.remoteTimestamp, third);

            // compute where we are half way between first and second,
            // and now are updated 1 minute later.
            double localTime = 103; // 1011+bufferTime so third snapshot is old enough
            double deltaTime = 98.5; // 99s - interpolation time
            double interpolationTime = 0.5; // half way between first and second
            float bufferTime = 2;
            int catchupThreshold = Int32.MaxValue;
            float catchupMultiplier = 0;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should spit out the interpolated snapshot
            Assert.That(result, Is.True);
            // interpolation started at 0.5, right between first & second.
            // we received another snapshot at t=101.
            // delta = 98.5 seconds
            // => interpolationTime = 99
            // => overshoots second goal, so we move to third goal and subtract 1
            // => so we should be at 98 now
            Assert.That(interpolationTime, Is.EqualTo(98));
            // we moved to the next snapshot. so only 2 should be in buffer now.
            Assert.That(buffer.Count, Is.EqualTo(2));
            // delta between second and third is 100.
            // interpolationTime is at 98
            // interpolationTime is relative to second.time
            // => InverseLerp(1, 101, 1 + 98) = 0.98
            // which is at 98% of the value
            // => Lerp(1, 101, 0.98): 101-1 is 100. 98% are 98. relative to '1'
            //    makes it 99.
            Assert.That(computed.value, Is.EqualTo(99).Within(Mathf.Epsilon));
        }

        // fourth step: catchup should be considered if buffer gets too large
        [Test]
        public void Compute_Step4_InterpolateWithCatchup()
        {
            // add two old enough snapshots
            // (localTime - bufferTime)
            SimpleSnapshot first = new SimpleSnapshot(0, 0, 1);
            SimpleSnapshot second = new SimpleSnapshot(1, 1, 2);
            buffer.Add(first.remoteTimestamp, first);
            buffer.Add(second.remoteTimestamp, second);

            // start applying 25% catchup per excess when > 2.
            int catchupThreshold = 2;
            float catchupMultiplier = 0.25f;

            // two excess snapshots to make sure that multiplier is accumulated
            SimpleSnapshot excess1 = new SimpleSnapshot(2, 2, 3);
            SimpleSnapshot excess2 = new SimpleSnapshot(3, 3, 4);
            buffer.Add(excess1.remoteTimestamp, excess1);
            buffer.Add(excess2.remoteTimestamp, excess2);

            // compute with initialized remoteTime and buffer time of 2 seconds
            // and a delta time to be sure that we move along it no matter what.
            double localTime = 3;
            double deltaTime = 0.5;
            double interpolationTime = 0;
            float bufferTime = 2;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should spit out the interpolated snapshot
            Assert.That(result, Is.True);
            // interpolation started just now, from 0.
            // and deltaTime is 0.5 + 50% catchup, so we should be at 0.75 now
            Assert.That(interpolationTime, Is.EqualTo(0.75));
            // buffer should be untouched, we are still interpolating between
            // the first two.
            Assert.That(buffer.Count, Is.EqualTo(4));
            // computed snapshot should be interpolated in 3/4 because
            // interpolationTime is at 3/4
            Assert.That(computed.value, Is.EqualTo(1.75).Within(Mathf.Epsilon));
        }

        // fifth step: interpolation time overshoots the end while waiting for
        //             more snapshots.
        //
        // IMPORTANT: we should NOT extrapolate & predict while waiting for more
        //            snapshots as this would introduce a whole range of issues:
        //            * player might be extrapolated WAY out if we wait for long
        //            * player might be extrapolated behind walls
        //            * once we receive a new snapshot, we would interpolate
        //              not from the last valid position, but from the
        //              extrapolated position. this could be ANYWHERE. the
        //              player might get stuck in walls, etc.
        //            => we are NOT doing client side prediction & rollback here
        //            => we are simply interpolating with known, valid positions
        //
        // NOTE: to reproduce the issue in a real example:
        //       * open mirror benchmark example
        //       * editor=host 1000+ monsters & deep profiling for LOW FPS
        //       * build=client
        //       * move around client
        //       * see it all over the place in editor because it extrapolates,
        //         ends up at the wrong start positions and gets worse from
        //         there.
        //
        // video: https://gyazo.com/8de68f0a821449d7b9a8424e2c9e3ff8
        // (or see Mirror/Docs/Screenshots/NT Snap. Interp./extrapolation issues)
        [Test]
        public void Compute_Step5_OvershootWithoutEnoughSnapshots()
        {
            // add two old enough snapshots
            // (localTime - bufferTime)
            SimpleSnapshot first = new SimpleSnapshot(0, 0, 1);
            SimpleSnapshot second = new SimpleSnapshot(1, 1, 2);
            buffer.Add(first.remoteTimestamp, first);
            buffer.Add(second.remoteTimestamp, second);

            // compute with initialized remoteTime and buffer time of 2 seconds
            // and a delta time to be sure that we move along it no matter what.
            // -> interpolation time is already at '1' at the end.
            // -> compute will add 0.5 deltaTime
            // -> so we should NOT overshoot aka extrapolate beyond second snap.
            double localTime = 3;
            double deltaTime = 0.5;
            double interpolationTime = 1;
            float bufferTime = 2;
            int catchupThreshold = Int32.MaxValue;
            float catchupMultiplier = 0;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should spit out the interpolated snapshot
            Assert.That(result, Is.True);
            // interpolation started at the end = 1
            // and deltaTime is 0.5, so it's at 1.5 internally.
            //
            // BUT there's NO reason to overshoot interpolationTime if there's
            // no other snapshots to move to.
            // interpolationTime overshoot is only for smooth transitions WHILE
            // moving.
            // for example, if we keep overshooting to 100, then we would
            // instantly skip the next 20 snapshots.
            // => so it should be capped at second.remoteTime
            Assert.That(interpolationTime, Is.EqualTo(1));
            // buffer should be untouched, we are still interpolating between the two
            Assert.That(buffer.Count, Is.EqualTo(2));
            // computed snapshot should NOT extrapolate beyond second snap.
            Assert.That(computed.value, Is.EqualTo(2).Within(Mathf.Epsilon));
        }

        // fifth step: interpolation time overshoots the end while having more
        //             snapshots available.
        //             BUT: the next snapshot isn't old enough yet.
        //                  we shouldn't move there until old enough.
        //                  for the same reason we don't move to first, second
        //                  until they are old enough.
        //                  => always need to be 'bufferTime' old.
        [Test]
        public void Compute_Step5_OvershootWithEnoughSnapshots_NextIsntOldEnough()
        {
            // add two old enough snapshots
            // (localTime - bufferTime)
            //
            // IMPORTANT: second.time needs to be != second.time-first.time
            //            to guarantee that we cap interpolationTime (which is
            //            RELATIVE from 0..delta) at delta, not at second.time.
            //            this was a bug before.
            SimpleSnapshot first = new SimpleSnapshot(1, 1, 1);
            SimpleSnapshot second = new SimpleSnapshot(2, 2, 2);
            // IMPORTANT: third snapshot needs to be:
            // - a different time delta
            //   to test if overflow is correct if deltas are different.
            //   it's not obvious if we ever use t ratio between [0,1] where an
            //   overflow of 0.1 between A,B could speed up B,C interpolation if
            //   that's not the same delta, since t is a ratio.
            // - a different value delta to check if it really _interpolates_,
            //   not just extrapolates further after A,B
            SimpleSnapshot third = new SimpleSnapshot(4, 4, 4);
            buffer.Add(first.remoteTimestamp, first);
            buffer.Add(second.remoteTimestamp, second);
            buffer.Add(third.remoteTimestamp, third);

            // compute with initialized remoteTime and buffer time of 2 seconds
            // and a delta time to be sure that we move along it no matter what.
            // -> interpolation time is already at '1' at the end.
            // -> compute will add 0.5 deltaTime
            // -> so we overshoot beyond the second one and move to the next
            //
            // localTime is at 4
            // third snapshot localTime is at 4.
            // bufferTime is 2, so it is NOT old enough and we should wait!
            double localTime = 4;
            double deltaTime = 0.5;
            double interpolationTime = 1;
            float bufferTime = 2;
            int catchupThreshold = Int32.MaxValue;
            float catchupMultiplier = 0;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should still spit out a result between first & second.
            Assert.That(result, Is.True);
            // interpolation started at the end = 1
            // and deltaTime is 0.5, so we were at 1.5 internally.
            //
            // BUT there's NO reason to overshoot interpolationTime while we
            // wait for the next snapshot which isn't old enough.
            // we stopped movement anyway.
            // interpolationTime overshoot is only for smooth transitions WHILE
            // moving.
            // for example, if we overshoot to 100 while waiting, then we would
            // instantly skip the next 20 snapshots.
            // => so it should be capped at max
            // => which is always 0..delta, NOT first.time .. second.time!!
            Assert.That(interpolationTime, Is.EqualTo(1));
            // buffer should be untouched. shouldn't have moved to third yet.
            Assert.That(buffer.Count, Is.EqualTo(3));
            // computed snapshot should be all the way at second snapshot.
            Assert.That(computed.value, Is.EqualTo(2).Within(Mathf.Epsilon));
        }

        // fifth step: interpolation time overshoots the end while having more
        //             snapshots available.
        [Test]
        public void Compute_Step5_OvershootWithEnoughSnapshots_MovesToNextSnapshotIfOldEnough()
        {
            // add two old enough snapshots
            // (localTime - bufferTime)
            SimpleSnapshot first = new SimpleSnapshot(0, 0, 1);
            SimpleSnapshot second = new SimpleSnapshot(1, 1, 2);
            // IMPORTANT: third snapshot needs to be:
            // - a different time delta
            //   to test if overflow is correct if deltas are different.
            //   it's not obvious if we ever use t ratio between [0,1] where an
            //   overflow of 0.1 between A,B could speed up B,C interpolation if
            //   that's not the same delta, since t is a ratio.
            // - a different value delta to check if it really _interpolates_,
            //   not just extrapolates further after A,B
            SimpleSnapshot third = new SimpleSnapshot(3, 3, 4);
            buffer.Add(first.remoteTimestamp, first);
            buffer.Add(second.remoteTimestamp, second);
            buffer.Add(third.remoteTimestamp, third);

            // compute with initialized remoteTime and buffer time of 2 seconds
            // and a delta time to be sure that we move along it no matter what.
            // -> interpolation time is already at '1' at the end.
            // -> compute will add 0.5 deltaTime
            // -> so we overshoot beyond the second one and move to the next
            //
            // localTime is 5. third snapshot localTime is at 3.
            // bufferTime is 2.
            // so third is exactly old enough and we should move there.
            double localTime = 5;
            double deltaTime = 0.5;
            double interpolationTime = 1;
            float bufferTime = 2;
            int catchupThreshold = Int32.MaxValue;
            float catchupMultiplier = 0;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should spit out the interpolated snapshot
            Assert.That(result, Is.True);
            // interpolation started at the end = 1
            // and deltaTime is 0.5, so we were at 1.5 internally.
            // we have more snapshots, so we jump to the next and subtract '1'
            // 1 + 0.5 = 1.5 => -1 => 0.5
            Assert.That(interpolationTime, Is.EqualTo(0.5));
            // buffer's first entry should have been removed
            Assert.That(buffer.Count, Is.EqualTo(2));
            // computed snapshot should be 1/4 way between second and third
            // because delta is 2 and interpolationTime is at 0.5 which is 1/4
            Assert.That(computed.value, Is.EqualTo(2.5).Within(Mathf.Epsilon));
        }

        // fifth step: interpolation time overshoots 2x the end while having
        //             >= 2 more snapshots available. it should correctly jump
        //             ahead the first pending one to the second one.
        [Test]
        public void Compute_Step5_OvershootWithEnoughSnapshots_2x_MovesToSecondNextSnapshot()
        {
            // add two old enough snapshots
            // (localTime - bufferTime)
            SimpleSnapshot first = new SimpleSnapshot(0, 0, 1);
            SimpleSnapshot second = new SimpleSnapshot(1, 1, 2);
            // IMPORTANT: third snapshot needs to be:
            // - a different time delta
            //   to test if overflow is correct if deltas are different.
            //   it's not obvious if we ever use t ratio between [0,1] where an
            //   overflow of 0.1 between A,B could speed up B,C interpolation if
            //   that's not the same delta, since t is a ratio.
            // - a different value delta to check if it really _interpolates_,
            //   not just extrapolates further after A,B
            SimpleSnapshot third = new SimpleSnapshot(3, 3, 4);
            SimpleSnapshot fourth = new SimpleSnapshot(5, 5, 6);
            buffer.Add(first.remoteTimestamp, first);
            buffer.Add(second.remoteTimestamp, second);
            buffer.Add(third.remoteTimestamp, third);
            buffer.Add(fourth.remoteTimestamp, fourth);

            // compute with initialized remoteTime and buffer time of 2 seconds
            // and a delta time to be sure that we move along it no matter what.
            // -> interpolation time is already at '1' at the end.
            // -> compute will add 1.5 deltaTime
            // -> so we should overshoot beyond second and third even
            //
            // localTime is 7. fourth snapshot localTime is at 5.
            // bufferTime is 2.
            // so fourth is exactly old enough and we should move there.
            double localTime = 7;
            double deltaTime = 2.5;
            double interpolationTime = 1;
            float bufferTime = 2;
            int catchupThreshold = Int32.MaxValue;
            float catchupMultiplier = 0;
            bool result = SnapshotInterpolation.Compute(localTime, deltaTime, ref interpolationTime, bufferTime, buffer, catchupThreshold, catchupMultiplier, SimpleSnapshot.Interpolate, out SimpleSnapshot computed);

            // should spit out the interpolated snapshot
            Assert.That(result, Is.True);
            // interpolation started at the end = 1
            // and deltaTime is 2.5, so we were at 4.5 internally.
            // we have more snapshots, so we:
            //   * jump to third, subtract delta of 1-0 = 1 => 2.5
            //   * jump to fourth, subtract delta of 3-1 = 2 => 0.5
            //   * end up at 0.5 again, between third and fourth
            Assert.That(interpolationTime, Is.EqualTo(0.5));
            // buffer's first entry should have been removed
            Assert.That(buffer.Count, Is.EqualTo(2));
            // computed snapshot should be 1/4 way between second and third
            // because delta is 2 and interpolationTime is at 0.5 which is 1/4
            Assert.That(computed.value, Is.EqualTo(4.5).Within(Mathf.Epsilon));
        }
    }
}
