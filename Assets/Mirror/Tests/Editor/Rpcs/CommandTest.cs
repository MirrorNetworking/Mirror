using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Rpcs
{
    class AuthorityBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [Command]
        public void SendInt(int someInt) =>
            onSendInt?.Invoke(someInt);
    }

    class IgnoreAuthorityBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [Command(requiresAuthority = false)]
        public void CmdSendInt(int someInt) =>
            onSendInt?.Invoke(someInt);
    }

    class SenderConnectionBehaviour : NetworkBehaviour
    {
        public event Action<int, NetworkConnectionToClient> onSendInt;

        [Command]
        public void CmdSendInt(int someInt, NetworkConnectionToClient conn = null) =>
            onSendInt?.Invoke(someInt, conn);
    }

    class SenderConnectionIgnoreAuthorityBehaviour : NetworkBehaviour
    {
        public event Action<int, NetworkConnectionToClient> onSendInt;

        [Command(requiresAuthority = false)]
        public void CmdSendInt(int someInt, NetworkConnectionToClient conn = null) =>
            onSendInt?.Invoke(someInt, conn);
    }

    class ThrowBehaviour : NetworkBehaviour
    {
        public const string ErrorMessage = "Bad things happened";

        [Command]
        public void SendThrow(int _) => throw new Exception(ErrorMessage);
    }

    class CommandOverloads : NetworkBehaviour
    {
        public int firstCalled = 0;
        public int secondCalled = 0;

        [Command]
        public void TheCommand(int _) => ++firstCalled;

        [Command]
        public void TheCommand(string _) => ++secondCalled;
    }

    public class CommandTest : MirrorTest
    {
        NetworkConnectionToClient connectionToClient;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // start server/client
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        [Test]
        public void CommandIsSentWithAuthority()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out AuthorityBehaviour serverComponent,
                                    out _, out _, out AuthorityBehaviour clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int callCount = 0;
            serverComponent.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            clientComponent.SendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void WarningForCommandSentWithoutAuthority()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out _, out _, out AuthorityBehaviour serverComponent,
                                    out _, out _, out AuthorityBehaviour clientComponent);

            const int someInt = 20;

            int callCount = 0;
            serverComponent.onSendInt += incomingInt =>
            {
                callCount++;
            };
            LogAssert.Expect(LogType.Warning, new Regex($".*without authority.*"));
            clientComponent.SendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.Zero);
        }


        [Test]
        public void CommandIsSentWithAuthorityWhenIgnoringAuthority()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out IgnoreAuthorityBehaviour serverComponent,
                                    out _, out _, out IgnoreAuthorityBehaviour clientComponent,
                                    connectionToClient);

            const int someInt = 20;

            int callCount = 0;
            serverComponent.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            clientComponent.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void CommandIsSentWithoutAuthorityWhenIgnoringAuthority()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out _, out _, out IgnoreAuthorityBehaviour serverComponent,
                                    out _, out _, out IgnoreAuthorityBehaviour clientComponent);

            const int someInt = 20;

            int callCount = 0;
            serverComponent.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            clientComponent.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void SenderConnectionIsSetWhenCommandIsRecieved()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out SenderConnectionBehaviour serverComponent,
                                    out _, out _, out SenderConnectionBehaviour clientComponent,
                                    connectionToClient);

            const int someInt = 20;
            int callCount = 0;
            serverComponent.onSendInt += (incomingInt, incomingConn) =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
                Assert.That(incomingConn, Is.EqualTo(connectionToClient));
            };
            clientComponent.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void SenderConnectionIsSetWhenCommandIsRecievedWithIgnoreAuthority()
        {
            // spawn without owner
            CreateNetworkedAndSpawn(out _, out _, out SenderConnectionIgnoreAuthorityBehaviour serverComponent,
                                    out _, out _, out SenderConnectionIgnoreAuthorityBehaviour clientComponent);

            const int someInt = 20;
            int callCount = 0;
            serverComponent.onSendInt += (incomingInt, incomingConn) =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
                Assert.That(incomingConn, Is.EqualTo(connectionToClient));
            };
            clientComponent.CmdSendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void CommandThatThrowsShouldBeCaught()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out ThrowBehaviour serverComponent,
                                    out _, out _, out ThrowBehaviour clientComponent,
                                    connectionToClient);

            const int someInt = 20;
            LogAssert.Expect(LogType.Error, new Regex($".*{ThrowBehaviour.ErrorMessage}.*"));
            Assert.DoesNotThrow(() =>
            {
                clientComponent.SendThrow(someInt);
                ProcessMessages();
            }, "Processing new message should not throw, the exception from SendThrow should be caught");
        }

        // RemoteCalls uses md.FullName which gives us the full command/rpc name
        // like "System.Void Mirror.Tests.RemoteAttrributeTest.AuthorityBehaviour::SendInt(System.Int32)"
        // which means overloads with same name but different types should work.
        [Test]
        public void CommandOverloads()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out CommandOverloads serverComponent,
                                    out _, out _, out CommandOverloads clientComponent,
                                    connectionToClient);

            // call both overloads once
            clientComponent.TheCommand(42);
            clientComponent.TheCommand("A");
            ProcessMessages();
            Assert.That(serverComponent.firstCalled, Is.EqualTo(1));
            Assert.That(serverComponent.secondCalled, Is.EqualTo(1));
        }
    }

    // need host mode for this one test
    public class CommandTest_HostMode : MirrorTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // start server/client
            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        // test to prevent https://github.com/vis2k/Mirror/issues/2629
        // from happening again in the future
        // -> [Command]s can be called on other objects with requiresAuthority=false.
        // -> those objects don't have a .connectionToServer
        // -> we broke it when using .connectionToServer instead of
        //    NetworkClient.connection in SendCommandInternal.
        [Test]
        public void Command_RequiresAuthorityFalse_ForOtherObjectWithoutConnectionToServer()
        {
            // spawn without owner (= without connectionToClient)
            CreateNetworkedAndSpawn(out _, out _, out IgnoreAuthorityBehaviour comp);

            // setup callback
            int called = 0;
            comp.onSendInt += _ => { ++called; };

            // call command. don't require authority.
            // the object doesn't have a .connectionToServer (like a scene object)
            Assert.That(comp.connectionToServer, Is.Null);
            comp.CmdSendInt(0);
            ProcessMessages();
            Assert.That(called, Is.EqualTo(1));
        }
    }
}
