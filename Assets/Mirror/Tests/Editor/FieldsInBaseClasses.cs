using System;
using Mirror.Tests.RemoteAttrributeTest;
using NUnit.Framework;
using UnityEngine;

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
        [Test, Ignore("Destroy is needed for the code. Can't be called in Edit mode.")]
        public void WriterShouldIncludeFieldsInBaseClass()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out DataSenderBehaviour hostBehaviour, NetworkServer.localConnection);

            const bool toggle = true;
            const int usefulNumber = 10;

            int called = 0;
            hostBehaviour.onData += data =>
            {
                called++;
                Assert.That(data.usefulNumber, Is.EqualTo(usefulNumber));
                Assert.That(data.toggle, Is.EqualTo(toggle));
            };
            hostBehaviour.CmdSendData(new SomeOtherData
            {
                usefulNumber = usefulNumber,
                toggle = toggle
            });

            ProcessMessages();
            Assert.That(called, Is.EqualTo(1));
        }
    }
}
