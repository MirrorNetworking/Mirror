using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkIdentitySorterTests : MirrorTest
    {
        NetworkIdentity A;
        NetworkIdentity B;
        NetworkIdentity C;
        NetworkIdentity D;
        NetworkIdentitySorter sorter;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);

            sorter = new NetworkIdentitySorter();

            // create networked AND SPAWN so that netId is set for sorter to use
            CreateNetworkedAndSpawn(out _, out A, out _, out _);
            A.transform.position = new Vector3(-2, -2, -2);

            CreateNetworkedAndSpawn(out _, out B, out _, out _);
            B.transform.position = new Vector3(-1, -1, -1);

            CreateNetworkedAndSpawn(out _, out C, out _, out _);
            C.transform.position = new Vector3(1, 1, 1);

            CreateNetworkedAndSpawn(out _, out D, out _, out _);
            D.transform.position = new Vector3(3, 3, 3);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void Compare()
        {
            sorter.Reset(Vector3.zero, 0);
            Assert.That(sorter.Compare(A, A), Is.EqualTo(0));  // compare with self should always be 0. only allow one entry.
            Assert.That(sorter.Compare(A, B), Is.EqualTo(1));  //  1 because A further from center than B
            Assert.That(sorter.Compare(A, C), Is.EqualTo(1));  //  1 because A further from center than C
            Assert.That(sorter.Compare(A, D), Is.EqualTo(-1)); // -1 because A closer to center than D

            Assert.That(sorter.Compare(B, A), Is.EqualTo(-1));
            Assert.That(sorter.Compare(B, B), Is.EqualTo(0));  // compare with self should always be 0. only allow one entry.
            Assert.That(sorter.Compare(B, C), !Is.EqualTo(0)); // same distance, falls back to hashcode comparison. should be != 0.
            Assert.That(sorter.Compare(B, D), Is.EqualTo(-1));

            Assert.That(sorter.Compare(C, A), Is.EqualTo(-1));
            Assert.That(sorter.Compare(C, B), !Is.EqualTo(0)); // same distance, falls back to hashcode comparison. should be != 0.
            Assert.That(sorter.Compare(C, C), Is.EqualTo(0));  // compare with self should always be 0. only allow one entry.
            Assert.That(sorter.Compare(C, D), Is.EqualTo(-1));

            Assert.That(sorter.Compare(D, A), Is.EqualTo(1));
            Assert.That(sorter.Compare(D, B), Is.EqualTo(1));
            Assert.That(sorter.Compare(D, C), Is.EqualTo(1));
            Assert.That(sorter.Compare(D, D), Is.EqualTo(0));  // compare with self should always be 0. only allow one entry.
        }

        // IMPORTANT
        // SortedSet does NOT allow two entries with same order.
        // one entry would be discarded automatically.
        // but we DO need to allow two NetworkIdentities at the same position.
        // => test guarantees that this is always supported.
        [Test]
        public void SamePosition_NotDiscarded()
        {
            sorter.Reset(Vector3.zero, 0);
            SortedSet<NetworkIdentity> sorted = new SortedSet<NetworkIdentity>(sorter);

            A.transform.position = B.transform.position;
            sorted.Add(A);
            sorted.Add(B);

            Assert.That(sorted.Count, Is.EqualTo(2));
        }

        // IMPORTANT
        // SortedSet does NOT allow two entries with same order.
        // one entry would be discarded automatically.
        // but we DO need to allow two NetworkIdentities at the same position.
        // => test guarantees that this is always supported.
        [Test]
        public void SameDistance_NotDiscarded()
        {
            sorter.Reset(Vector3.zero, 0);
            SortedSet<NetworkIdentity> sorted = new SortedSet<NetworkIdentity>(sorter);

            // above test checks same POSITION
            // but it's also possible that two entities are different positions
            // but same DISTANCE to the comparing position.
            A.transform.position = -B.transform.position;
            sorted.Add(A);
            sorted.Add(B);

            Assert.That(sorted.Count, Is.EqualTo(2));
        }

        // make sure that in a list of 4 entities where 2 have the same distance,
        // the order is still correct even if we fall back to hashcode for the two.
        // => the two fallbacks shouldn't suddenly be out of order compared to
        //    the others.
        [Test]
        public void SameDistance_NextToOthers()
        {
            sorter.Reset(Vector3.zero, 0);
            SortedSet<NetworkIdentity> sorted = new SortedSet<NetworkIdentity>(sorter);

            // B & C should have the same distance to make sure we trigger the
            // edge case.
            float bDistance = Vector3.Distance(B.transform.position, Vector3.zero);
            float cDistance = Vector3.Distance(C.transform.position, Vector3.zero);
            Assert.That(bDistance, Is.EqualTo(cDistance));

            // add all
            sorted.Add(A);
            sorted.Add(B);
            sorted.Add(C);
            sorted.Add(D);

            // B and C have same distance so should fall back being sorted by
            // hashcode.
            // => B and C are closest. depending on hashcode it's BC or CB
            // => then C because same distance but larger netId
            // => then A
            // => then D
            Assert.That(sorted.Count, Is.EqualTo(4));
            Assert.That(sorted.SequenceEqual(new []{B, C, A, D}) ||
                        sorted.SequenceEqual(new []{C, B, A, D}));
        }

        // guarantee that adding the same NetworkIdentity only keeps one entry!
        // our sorter has some custom magic, so let's be sure.
        [Test]
        public void SortedSet_NoDuplicates()
        {
            sorter.Reset(Vector3.zero, 0);
            SortedSet<NetworkIdentity> sorted = new SortedSet<NetworkIdentity>(sorter);
            sorted.Add(A);
            sorted.Add(A);
            Assert.That(sorted.Count, Is.EqualTo(1));
        }

        // need to guarantee that player is ALWAYS included.
        // even when standing on a spawn point with 1000 other players at the
        // exact same position.
        [Test]
        public void PlayerAlwaysHighestPriority()
        {
            // set to positions 1,2,3,4
            A.transform.position = new Vector3(1, 0, 0);
            B.transform.position = new Vector3(2, 0, 0);
            C.transform.position = new Vector3(3, 0, 0);
            D.transform.position = new Vector3(4, 0, 0);

            // set sort position to 0.
            // set player to C.
            sorter.Reset(Vector3.zero, C.netId);

            // sort. player should still be highest priority.
            SortedSet<NetworkIdentity> sorted = new SortedSet<NetworkIdentity>(sorter);
            sorted.Add(A);
            sorted.Add(B);
            sorted.Add(C);
            sorted.Add(D);
            Assert.That(sorted.SequenceEqual(new []{C, A, B, D}));
        }
    }
}
