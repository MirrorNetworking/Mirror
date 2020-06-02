using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class AuthorityBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [Command]
        public void CmdSendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    class IgnoreAuthorityBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [Command(ignoreAuthority = true)]
        public void CmdSendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    public class CommandTest : RemoteTestBase
    {
        [Test]
        public void CommandIsSentWithAuthority()
        {
            AuthorityBehaviour hostBehaviour = CreateHostObject<AuthorityBehaviour>(true);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void WarningForCommandSentWithoutAuthority()
        {
            AuthorityBehaviour hostBehaviour = CreateHostObject<AuthorityBehaviour>(false);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
            };
            LogAssert.Expect(LogType.Warning, $"Trying to send command for object without authority. {typeof(AuthorityBehaviour).ToString()}.{nameof(AuthorityBehaviour.CmdSendInt)}");
            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.Zero);
        }


        [Test]
        public void CommandIsSentWithAuthorityWhenIgnoringAuthority()
        {
            IgnoreAuthorityBehaviour hostBehaviour = CreateHostObject<IgnoreAuthorityBehaviour>(true);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void CommandIsSentWithoutAuthorityWhenIgnoringAuthority()
        {
            IgnoreAuthorityBehaviour hostBehaviour = CreateHostObject<IgnoreAuthorityBehaviour>(false);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }
    }
}
