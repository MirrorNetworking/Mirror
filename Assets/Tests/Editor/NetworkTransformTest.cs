using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkTransformTest
    {
        [Test]
        public void SerializeIntoWriterTest()
        {
            var writer = new NetworkWriter();
            var position = new Vector3(1, 2, 3);
            var rotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f);
            var scale = new Vector3(0.5f, 0.6f, 0.7f);

            NetworkTransformBase.SerializeIntoWriter(writer, position, rotation, scale);
            var reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadVector3(), Is.EqualTo(position));
            Assert.That(reader.ReadQuaternion(), Is.EqualTo(rotation));
            Assert.That(reader.ReadVector3(), Is.EqualTo(scale));
        }
    }
}
