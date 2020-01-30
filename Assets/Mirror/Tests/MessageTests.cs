using UnityEngine;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class MessageTests : MonoBehaviour
    {
        [Test]
        public void ErrorMessageTest()
        {
            // try setting value with constructor
            ErrorMessage errorMessage = new ErrorMessage(123);
            Assert.That(errorMessage.value, Is.EqualTo(123));

            // try deserialize
            byte[] data = { 123 };
            errorMessage.Deserialize(new NetworkReader(data));
            Assert.That(errorMessage.value, Is.EqualTo(123));

            // try serialize
            NetworkWriter writer = new NetworkWriter();
            errorMessage.Serialize(writer);
            Assert.That(writer.ToArray()[0], Is.EqualTo(123));
        }
    }
}
