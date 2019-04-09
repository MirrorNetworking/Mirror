using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class CustomWriterTest
    {
        public class MyClass
        {
            public string name {get; set; }
        }

        [Writer]
        public static void WriteMyClass(NetworkWriter networkWriter, MyClass obj)
        {
            networkWriter.Write(obj.name);
        }

        [Reader]
        public static MyClass ReadMyClass(NetworkReader reader)
        {
            return new MyClass()
            {
                name = reader.ReadString()
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
                name = "Hello World"
            };

            MyMessage message = new MyMessage()
            {
                myobj = obj
            };

            byte[] data = MessagePacker.Pack(message);

            MyMessage unpacked = MessagePacker.Unpack<MyMessage>(data);

            Assert.That(unpacked.myobj.name, Is.EqualTo("Hello World"), "Should be able to use user provider reader/writer");
        }
    }
}
