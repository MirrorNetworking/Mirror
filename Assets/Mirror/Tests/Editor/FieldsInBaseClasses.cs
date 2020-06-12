using System;
using Mirror.Tests.RemoteAttrributeTest;
using NUnit.Framework;

namespace Mirror.Tests.GeneratedWriterTests
{
    public class BaseData
    {
        public bool toggle;
    }
    public class SomeOtherData : BaseData
    {
        public int usefulNumber;
    }

    public class DataSenderBehaviour : NetworkBehaviour
    {
        public event Action<SomeOtherData> onData;
        [Command]
        public void CmdSendData(SomeOtherData otherData)
        {
            onData?.Invoke(otherData);
        }
    }

    public class FieldsInBaseClasses : RemoteTestBase
    {
        [Test]
        public void WriterShouldIncludeFieldsInBaseClass()
        {
            DataSenderBehaviour hostBehaviour = CreateHostObject<DataSenderBehaviour>(true);

            const bool toggle = true;
            const int usefulNumber = 10;

            int callCount = 0;
            hostBehaviour.onData += data =>
            {
                callCount++;
                Assert.That(data.usefulNumber, Is.EqualTo(usefulNumber));
                Assert.That(data.toggle, Is.EqualTo(toggle));
            };
            hostBehaviour.CmdSendData(new SomeOtherData
            {
                usefulNumber = usefulNumber,
                toggle = toggle
            });

            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }
    }
}
