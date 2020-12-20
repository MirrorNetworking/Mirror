using System.Collections;
using System.Collections.Generic;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.TransformSyncing.Tests
{
    public class NetworkTransformSystemTests
    {
        static IEnumerable CompressesAndDecompressesCases()
        {
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(0, 0, 0));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(20, 20, 20));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(50, 50, 50));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(100, 100, 100));
            yield return new TestCaseData(new Vector3(0, -20, 0), new Vector3(500, 60, 500), 0.031f, new Vector3(250, 10, 250));
        }


        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void CanSynOneObjectPosition(Vector3 min, Vector3 max, float precision, Vector3 inValue)
        {
            NetworkTransformSystem system = new NetworkTransformSystem();
            system.compression = new PositionCompression(min, max, precision);

            IHasPositionRotation hasPos = Substitute.For<IHasPositionRotation>();
            hasPos.PositionRotation.Returns(new PositionRotation(inValue, Quaternion.identity));
            hasPos.Id.Returns(1u);
            hasPos.NeedsUpdate(Arg.Any<float>()).Returns(true);

            system.AddBehaviour(hasPos);

            NetworkPositionMessage msg;
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                msg = system.CreateSendToAllMessage(writer);
            }

            int bytesPerObject = 1 + Mathf.CeilToInt(system.compression.bitCount / 8f) + 4;
            Assert.That(msg.bytes.Count, Is.EqualTo(bytesPerObject));

            system.ClientHandleNetworkPositionMessage(null, msg);

            hasPos.Received(1).ApplyOnClient(Arg.Is<PositionRotation>(v => Vector3AlmostEqual(v.position, inValue, precision)));
        }


        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void CanSyncFiveObjectPosition(Vector3 min, Vector3 max, float precision, Vector3 inValue)
        {
            NetworkTransformSystem system = new NetworkTransformSystem();
            system.compression = new PositionCompression(min, max, precision);

            List<IHasPositionRotation> hasPoss = new List<IHasPositionRotation>();
            for (int i = 0; i < 5; i++)
            {
                IHasPositionRotation hasPos = Substitute.For<IHasPositionRotation>();
                hasPos.PositionRotation.Returns(new PositionRotation(inValue, Quaternion.identity));
                hasPos.Id.Returns((uint)(i + 1));
                hasPos.NeedsUpdate(Arg.Any<float>()).Returns(true);

                hasPoss.Add(hasPos);
                system.AddBehaviour(hasPos);
            }

            NetworkPositionMessage msg;
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                msg = system.CreateSendToAllMessage(writer);
            }

            int bytesPerObject = 1 + Mathf.CeilToInt(system.compression.bitCount / 8f) + 4;
            Assert.That(msg.bytes.Count, Is.EqualTo(bytesPerObject * 5));

            system.ClientHandleNetworkPositionMessage(null, msg);

            for (int i = 0; i < 5; i++)
            {
                IHasPositionRotation hasPos = hasPoss[i];
                hasPos.Received(1).ApplyOnClient(Arg.Is<PositionRotation>(v => Vector3AlmostEqual(v.position, inValue, precision)));
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
