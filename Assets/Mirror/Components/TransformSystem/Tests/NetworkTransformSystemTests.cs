using System;
using System.Collections;
using System.Collections.Generic;
using JamesFrowen.BitPacking;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Mirror.TransformSyncing.Tests
{
    public class NetworkTransformSystemFuzzyTests
    {
        [Test]
        [Repeat(100)]
        public void CanSyncRandomValuesRepeat()
        {
            NetworkTransformSystem system = new NetworkTransformSystem();
            NetworkTransformSystemRuntimeReference runtime = new NetworkTransformSystemRuntimeReference();
            system.runtime = runtime;

            int smallBit = Random.Range(3, 10);
            int mediumBit = smallBit + Random.Range(3, 10);
            int largeBit = mediumBit + Random.Range(3, 10);
            system.idPacker = new UIntVariablePacker(smallBit, mediumBit, largeBit);

            float timeMax = Random.Range(1000, 2_000_000);
            float timeprecision = Random.Range(1 / 10000f, 1 / 10f);
            system.timePacker = new FloatPacker(0, timeMax, timeprecision);


            Vector3 posMin = RandomVector(-100, 100);
            Vector3 posMax = posMin + RandomVector(10, 1000);
            float posPrecsion = Random.Range(1 / 10000f, 2);
            system.positionPacker = new PositionPacker(posMin, posMax, posPrecsion);

            int rotationBits = Random.Range(7, 14);
            system.rotationPacker = new QuaternionPacker(rotationBits);


            int numberOfObjects = Random.Range(1, 100);


            // todo check correct number of bits are written

            float timeNow = Random.Range(0, timeMax);
            uint idOffset = (uint)Random.Range(1, Mathf.Max(2, (int)(system.idPacker.MaxValue - numberOfObjects)));

            List<IHasPositionRotation> hasPoss = new List<IHasPositionRotation>();
            for (int i = 0; i < numberOfObjects; i++)
            {
                Vector3 pos = RandomPointInBounds(posMin, posMax);
                Quaternion rot = Random.rotation;

                uint id = idOffset + (uint)i;

                IHasPositionRotation hasPos = Substitute.For<IHasPositionRotation>();
                hasPos.State.Returns(new TransformState(id, pos, rot));
                hasPos.NeedsUpdate().Returns(true);

                hasPoss.Add(hasPos);
                runtime.AddBehaviour(hasPos);
            }

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                system.PackBehaviours(writer);
                ArraySegment<byte> payload = writer.ToArraySegment();

                system.ClientHandleNetworkPositionMessage(null, new NetworkPositionMessage { bytes = payload });
            }

            // this isnt exact precision but it should be greater than real precision
            float rotPrecision = 2f / (1 << system.rotationPacker.bitCount);
            for (int i = 0; i < numberOfObjects; i++)
            {
                IHasPositionRotation hasPos = hasPoss[i];
                TransformState state = hasPos.State;
                hasPos.Received(1).ApplyOnClient(Arg.Is<TransformState>(arg => AreAlmostEqual(arg, state, posPrecsion, rotPrecision)), timeNow);
            }
        }

        private static bool AreAlmostEqual(TransformState arg, TransformState state, float posPrecsion, float rotPrecision)
        {
            bool posEqual = NetworkTransformSystemTests.Vector3AlmostEqual(arg.position, state.position, posPrecsion);
            bool rotEqual = NetworkTransformSystemTests.QuaternionAlmostEqual(arg.rotation, state.rotation, rotPrecision);
            if (!posEqual)
            {
                Debug.LogError($"Position Not Equal A:{arg.position}, B:{state.rotation}, P:{posPrecsion}, D:{state.position - arg.position}");
            }
            if (!rotEqual)
            {
                Debug.LogError($"Rotation Not Equal A:{arg.rotation}, B:{state.rotation}, P:{rotPrecision}, A:{Quaternion.Angle(arg.rotation, state.rotation)}");
            }
            return posEqual && rotEqual;
        }

        Vector3 RandomVector(float min, float max)
        {
            return new Vector3(
                Random.Range(min, max),
                Random.Range(min, max),
                Random.Range(min, max));
        }
        Vector3 RandomPointInBounds(Vector3 min, Vector3 max)
        {
            return new Vector3(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                Random.Range(min.z, max.z)
            );
        }
    }
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


        public static bool Vector3AlmostEqual(Vector3 actual, Vector3 expected, float precision)
        {
            return FloatAlmostEqual(actual.x, expected.x, precision)
                && FloatAlmostEqual(actual.y, expected.y, precision)
                && FloatAlmostEqual(actual.z, expected.z, precision);
        }

        public static bool FloatAlmostEqual(float actual, float expected, float precision)
        {
            float minAllowed = expected - precision;
            float maxnAllowed = expected + precision;

            return minAllowed < actual && actual < maxnAllowed;
        }

        public static bool QuaternionAlmostEqual(Quaternion actual, Quaternion expected, float precision)
        {
            return Quaternion.Angle(actual, expected) < precision;
        }
    }
}
