using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class CustomWriterTest
    {
        public class MyClass
        {
            public string Name { get; set; }
        }

        [Writer]
        public static void WriteMyClass(NetworkWriter networkWriter, MyClass obj)
        {
            networkWriter.Write(obj.Name);
        }

        [Reader]
        public static MyClass ReadMyClass(NetworkReader reader)
        {
            return new MyClass()
            {
                Name = reader.ReadString()
            };
        }

        class MyMessage : MessageBase
        {
            public MyClass myobj;
        }

        [Test]
        public void TestCustomWriter()
        {
            MyClass obj = new MyClass()
            {
                Name = "Hello World"
            };

            MyMessage message = new MyMessage()
            {
                myobj = obj
            };

            byte[] data = MessagePacker.Pack(message);

            MyMessage unpacked = MessagePacker.Unpack<MyMessage>(data);

            Assert.That(unpacked.myobj.Name, Is.EqualTo("Hello World"), "Should be able to use user provider reader/writer");
        }
    }
}
