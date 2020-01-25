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
            NetworkWriter writer = new NetworkWriter();
            Vector3 position = new Vector3(1, 2, 3);
            Quaternion rotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f);
            Vector3 scale = new Vector3(0.5f, 0.6f, 0.7f);

            // Compression.None
            NetworkTransformBase.SerializeIntoWriter(writer, position, rotation, NetworkTransformBase.Compression.None, scale);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadVector3(), Is.EqualTo(position));
            Assert.That(reader.ReadVector3(), Is.EqualTo(rotation.eulerAngles));
            Assert.That(reader.ReadVector3(), Is.EqualTo(scale));
        }
    }
}
