using System;
using System.Collections;
using System.Collections.Generic;
using JamesFrowen.BitPacking;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.TransformSyncing.Tests
{
    public class NetworkTransformSystemTests
    {
        const int smallIntBitCount = 7;
        private NetworkTransformSystem system;
        private NetworkTransformSystemRuntimeReference runtime;

        static IEnumerable CompressesAndDecompressesCases()
        {
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(0, 0, 0));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(20, 20, 20));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(50, 50, 50));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(100, 100, 100));
            yield return new TestCaseData(new Vector3(0, -20, 0), new Vector3(500, 60, 500), 0.031f, new Vector3(250, 10, 250));
        }

        [SetUp]
        public void SetUp()
        {
            system = new NetworkTransformSystem();
            runtime = new NetworkTransformSystemRuntimeReference();
            system.runtime = runtime;
            // -1 because first bit is to say it is small
            system.idPacker = new UIntVariablePacker(smallIntBitCount - 1, 12, 18);
            system.timePacker = new FloatPacker(0, 3600f, 1 / 1000f);
            system.rotationPacker = new QuaternionPacker(9);
        }

        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void CanSynOneObjectPosition(Vector3 min, Vector3 max, float precision, Vector3 inValue)
        {
            system.positionPacker = new PositionPacker(min, max, precision);
            int timeBits = system.timePacker.bitCount;
            int bitsPerObject = smallIntBitCount + system.positionPacker.bitCount + system.rotationPacker.bitCount;
            int totalBits = timeBits + bitsPerObject;
            int flushBits = 8 - (totalBits % 8);
            if (flushBits == 8) flushBits = 0;

            IHasPositionRotation hasPos = Substitute.For<IHasPositionRotation>();
            hasPos.State.Returns(new TransformState(1u, inValue, Quaternion.identity));
            hasPos.NeedsUpdate().Returns(true);

            runtime.AddBehaviour(hasPos);

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                system.PackBehaviours(writer);
                ArraySegment<byte> payload = writer.ToArraySegment();

                int expectedByteCount = Mathf.CeilToInt(totalBits / 8f);
                Assert.That(payload.Count, Is.EqualTo(expectedByteCount));

                system.ClientHandleNetworkPositionMessage(null, new NetworkPositionMessage { bytes = payload });
                Assert.That(system.bitReader.BitsInScratch, Is.EqualTo(flushBits), "should have read exact amount");
            }

            hasPos.Received(1).ApplyOnClient(Arg.Is<TransformState>(v => Vector3AlmostEqual(v.position, inValue, precision)), Arg.Any<float>());
        }


        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void CanSyncFiveObjectPosition(Vector3 min, Vector3 max, float precision, Vector3 inValue)
        {
            system.positionPacker = new PositionPacker(min, max, precision);
            int timeBits = system.timePacker.bitCount;
            int bitsPerObject = smallIntBitCount + system.positionPacker.bitCount + system.rotationPacker.bitCount;
            int totalBits = timeBits + bitsPerObject * 5;
            int flushBits = 8 - (totalBits % 8);
            if (flushBits == 8) flushBits = 0;

            List<IHasPositionRotation> hasPoss = new List<IHasPositionRotation>();
            for (int i = 0; i < 5; i++)
            {
                IHasPositionRotation hasPos = Substitute.For<IHasPositionRotation>();
                hasPos.State.Returns(new TransformState((uint)(i + 1), inValue, Quaternion.identity));
                hasPos.NeedsUpdate().Returns(true);

                hasPoss.Add(hasPos);
                runtime.AddBehaviour(hasPos);
            }

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                system.PackBehaviours(writer);
                ArraySegment<byte> payload = writer.ToArraySegment();

                int expectedByteCount = Mathf.CeilToInt(totalBits / 8f);
                Assert.That(payload.Count, Is.EqualTo(expectedByteCount));

                system.ClientHandleNetworkPositionMessage(null, new NetworkPositionMessage { bytes = payload });
                Assert.That(system.bitReader.BitsInScratch, Is.EqualTo(flushBits), "should have read exact amount");
            }

            for (int i = 0; i < 5; i++)
            {
                IHasPositionRotation hasPos = hasPoss[i];
                hasPos.Received(1).ApplyOnClient(Arg.Is<TransformState>(v => Vector3AlmostEqual(v.position, inValue, precision)), Arg.Any<float>());
            }
        }

        bool Vector3AlmostEqual(Vector3 actual, Vector3 expected, float precision)
        {
            return FloatAlmostEqual(actual.x, expected.x, precision)
                && FloatAlmostEqual(actual.y, expected.y, precision)
                && FloatAlmostEqual(actual.z, expected.z, precision);
        }

        bool FloatAlmostEqual(float actual, float expected, float precision)
        {
            float minAllowed = expected - precision;
            float maxnAllowed = expected + precision;

            return minAllowed < actual && actual < maxnAllowed;
        }
    }
}
