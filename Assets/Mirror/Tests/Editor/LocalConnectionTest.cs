using System;
using System.Net;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [Category("LocalConnection")]
    public class LocalConnectionTest
    {

        /*class MyMessage : MessageBase
        {
            public int id;
            public string name;
        }*/

        ULocalConnectionToClient connectionToClient;
        ULocalConnectionToServer connectionToServer;

        [SetUp]
        public void SetUpConnections()
        {
            (connectionToServer, connectionToClient) = ULocalConnectionToClient.CreateLocalConnections();
        }

        [TearDown]
        public void Disconnect()
        {
            connectionToServer.Disconnect();
        }

        [Test]
        public void LocalConnectionToClientAddressTest()
        {
            Assert.That(connectionToClient.Address, Is.EqualTo(new IPEndPoint(IPAddress.Loopback, 0)));
        }

        [Test]
        public void LocalConnectionToServerAddressTest()
        {
            Assert.That(connectionToServer.Address, Is.EqualTo(new IPEndPoint(IPAddress.Loopback, 0)));
        }

        [Test]
        public void ClientToServerFailTest()
        {
            Assert.Throws<InvalidMessageException>(() => connectionToServer.Send(new ArraySegment<byte>(new byte[0])));
        }
    }
}
