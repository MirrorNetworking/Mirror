using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class AuthorityBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [Command]
        public void SendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    class IgnoreAuthorityBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [Command(requiresAuthority = false)]
        public void CmdSendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    class SenderConnectionBehaviour : NetworkBehaviour
    {
        public event Action<int, NetworkConnection> onSendInt;

        [Command]
        public void CmdSendInt(int someInt, NetworkConnectionToClient conn = null)
        {
            onSendInt?.Invoke(someInt, conn);
        }
    }

    class SenderConnectionIgnoreAuthorityBehaviour : NetworkBehaviour
    {
        public event Action<int, NetworkConnection> onSendInt;

        [Command(requiresAuthority = false)]
        public void CmdSendInt(int someInt, NetworkConnectionToClient conn = null)
        {
            onSendInt?.Invoke(someInt, conn);
        }
    }

    class ThrowBehaviour : NetworkBehaviour
    {
        public const string ErrorMessage = "Bad things happened";

        [Command]
        public void SendThrow(int someInt)
        {
            throw new Exception(ErrorMessage);
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
            hostBehaviour.SendInt(someInt);
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
            LogAssert.Expect(LogType.Warning, $"Trying to send command for object without authority. {typeof(AuthorityBehaviour).ToString()}.{nameof(AuthorityBehaviour.SendInt)}");
            hostBehaviour.SendInt(someInt);
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

        [Test]
        public void SenderConnectionIsSetWhenCommandIsRecieved()
        {
            SenderConnectionBehaviour hostBehaviour = CreateHostObject<SenderConnectionBehaviour>(true);

            const int someInt = 20;
            NetworkConnectionToClient connectionToClient = NetworkServer.connections[0];
            Debug.Assert(connectionToClient != null, $"connectionToClient was null, This means that the test is broken and will give the wrong results");


            int callCount = 0;
            hostBehaviour.onSendInt += (incomingInt, incomingConn) =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
                Assert.That(incomingConn, Is.EqualTo(connectionToClient));
            };
            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void SenderConnectionIsSetWhenCommandIsRecievedWithIgnoreAuthority()
        {
            SenderConnectionIgnoreAuthorityBehaviour hostBehaviour = CreateHostObject<SenderConnectionIgnoreAuthorityBehaviour>(false);

            const int someInt = 20;
            NetworkConnectionToClient connectionToClient = NetworkServer.connections[0];
            Debug.Assert(connectionToClient != null, $"connectionToClient was null, This means that the test is broken and will give the wrong results");

            int callCount = 0;
            hostBehaviour.onSendInt += (incomingInt, incomingConn) =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
                Assert.That(incomingConn, Is.EqualTo(connectionToClient));
            };
            hostBehaviour.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void CommandThatThrowsShouldBeCaught()
        {
            ThrowBehaviour hostBehaviour = CreateHostObject<ThrowBehaviour>(true);

            const int someInt = 20;
            NetworkConnectionToClient connectionToClient = NetworkServer.connections[0];
            Debug.Assert(connectionToClient != null, $"connectionToClient was null, This means that the test is broken and will give the wrong results");

            LogAssert.Expect(LogType.Error, new Regex($".*{ThrowBehaviour.ErrorMessage}.*"));
            Assert.DoesNotThrow(() =>
            {
                hostBehaviour.SendThrow(someInt);
                ProcessMessages();
            }, "Processing new message should not throw, the exception from SendThrow should be caught");
        }
    }
}
