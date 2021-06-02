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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out AuthorityBehaviour hostBehaviour, NetworkServer.localConnection);

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
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out AuthorityBehaviour hostBehaviour);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out IgnoreAuthorityBehaviour hostBehaviour, NetworkServer.localConnection);

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
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out IgnoreAuthorityBehaviour hostBehaviour);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out SenderConnectionBehaviour hostBehaviour, NetworkServer.localConnection);

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
            // spawn without owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out SenderConnectionIgnoreAuthorityBehaviour hostBehaviour);

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
            // spawn with owner
            CreateNetworkedAndSpawn(out GameObject _, out NetworkIdentity _, out ThrowBehaviour hostBehaviour, NetworkServer.localConnection);

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
