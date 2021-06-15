using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkClientTests : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // we need a server to connect to
            NetworkServer.Listen(10);
        }

        [Test]
        public void ServerIp()
        {
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.serverIp, Is.EqualTo("localhost"));
        }

        [Test]
        public void IsConnected()
        {
            Assert.That(NetworkClient.isConnected, Is.False);
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.isConnected, Is.True);
        }

        [Test]
        public void ConnectUri()
        {
            NetworkClient.Connect(new Uri("memory://localhost"));
            // update transport so connect event is processed
            UpdateTransport();
            Assert.That(NetworkClient.isConnected, Is.True);
        }

        [Test]
        public void DisconnectInHostMode()
        {
            NetworkClient.ConnectHost();
            Assert.That(NetworkClient.isConnected, Is.True);
            Assert.That(NetworkServer.localConnection, !Is.Null);

            NetworkClient.Disconnect();
            Assert.That(NetworkClient.isConnected, Is.False);
            Assert.That(NetworkServer.localConnection, Is.Null);
        }

        // TODO flaky
        // TODO running play mode tests, then edit mode tests, makes this fail
        // TODO when running the multi scene example first, we get this error
        //      when running this test afterwards:
        //    Send (0.020s)
        //    ---
        //    UnityEngine.MissingReferenceException : The object of type 'MultiSceneNetManager' has been destroyed but you are still trying to access it.
        //    Your script should either check if it is null or you should not destroy the object.
        //    ---
        //    at (wrapper managed-to-native) UnityEngine.Component.get_gameObject(UnityEngine.Component)
        //      at Mirror.NetworkManager.StopClient () [0x0003c] in /Users/qwerty/x/dev/project_Mirror/Repository/Assets/Mirror/Runtime/NetworkManager.cs:595
        //      at Mirror.NetworkManager.OnClientDisconnect (Mirror.NetworkConnection conn) [0x00001] in /Users/qwerty/x/dev/project_Mirror/Repository/Assets/Mirror/Runtime/NetworkManager.cs:1260
        //      at Mirror.NetworkManager.OnClientDisconnectInternal () [0x00001] in /Users/qwerty/x/dev/project_Mirror/Repository/Assets/Mirror/Runtime/NetworkManager.cs:1167
        //      at Mirror.NetworkClient.OnTransportDisconnected () [0x00027] in /Users/qwerty/x/dev/project_Mirror/Repository/Assets/Mirror/Runtime/NetworkClient.cs:326
        //      at Mirror.Tests.MemoryTransport.ClientEarlyUpdate () [0x00087] in /Users/qwerty/x/dev/project_Mirror/Repository/Assets/Mirror/Tests/Common/MemoryTransport.cs:109
        //      at Mirror.Tests.MirrorTest.UpdateTransport () [0x00001] in /Users/qwerty/x/dev/project_Mirror/Repository/Assets/Mirror/Tests/Common/MirrorTest.cs:143
        //      at Mirror.Tests.NetworkClientTests.Send () [0x00043] in /Users/qwerty/x/dev/project_Mirror/Repository/Assets/Mirror/Tests/Editor/NetworkClientTests.cs:80
        //      at (wrapper managed-to-native) System.Reflection.MonoMethod.InternalInvoke(System.Reflection.MonoMethod,object,object[],System.Exception&)
        //      at System.Reflection.MonoMethod.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) [0x00032] in <eae584ce26bc40229c1b1aa476bfa589>:0
        //    ---
        [Test]
        public void Send()
        {
            // register server handler
            int called = 0;
            NetworkServer.RegisterHandler<AddPlayerMessage>((conn, msg) => { ++called; }, false);

            // connect a regular connection. not host, because host would use
            // connId=0 but memorytransport uses connId=1
            NetworkClient.Connect("localhost");
            // update transport so connect event is processed
            UpdateTransport();

            // send it
            AddPlayerMessage message = new AddPlayerMessage();
            NetworkClient.Send(message);

            // update client & server so batches are flushed
            NetworkClient.NetworkLateUpdate();
            NetworkServer.NetworkLateUpdate();

            // update transport so data event is processed
            UpdateTransport();

            // received it on server?
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void ShutdownCleanup()
        {
            // add some test event hooks to make sure they are cleaned up.
            // there used to be a bug where they wouldn't be cleaned up.
            NetworkClient.OnConnectedEvent = () => {};
            NetworkClient.OnDisconnectedEvent = () => {};

            NetworkClient.Shutdown();

            Assert.That(NetworkClient.OnConnectedEvent, Is.Null);
            Assert.That(NetworkClient.OnDisconnectedEvent, Is.Null);
        }
    }
}
