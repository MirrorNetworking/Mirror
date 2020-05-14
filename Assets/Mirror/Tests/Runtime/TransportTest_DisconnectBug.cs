using System;
using System.Collections;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Telepathy.Tests.Runtime
{
    [TestFixture]
    [Category("Telepathy")]
    [Ignore("Telepathy are unstable")]
    public class TransportTest_DisconnectBug
    {
        // just a random port that will hopefully not be taken
        const int goodPort = 7777;
        const int badPort = 7779;
        private const string localHostIp = "127.0.0.1";
        private const string localHost = "localhost";
        private TelepathyTransport clientTransport;
        private NetworkManager manager;
        private TelepathyTransport serverTransport;


        bool success;
        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            clientTransport = new GameObject().AddComponent<TelepathyTransport>();
            manager = clientTransport.gameObject.AddComponent<NetworkManager>();
            manager.startOnHeadless = false;
            manager.showDebugMessages = true;
            LogFilter.Debug = true;
            if (LogFilter.Debug)
            {
                LogFactory.EnableDebugMode();
            }
            Transport.activeTransport = clientTransport;

            serverTransport = new GameObject().AddComponent<TelepathyTransport>();

            serverTransport.port = goodPort;

            yield return null;
            serverTransport.ServerStart();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            serverTransport.ServerStop();
            NetworkManager.Shutdown();
            yield return null;
            GameObject.Destroy(clientTransport.gameObject);
            GameObject.Destroy(serverTransport.gameObject);
        }

        [UnityTest]
        public IEnumerator CanConnectToServer_Ip()
        {
            yield return CanConnectToServer(localHostIp);

            Assert.IsTrue(success, "Connection closed early");
        }
        [UnityTest]
        public IEnumerator CanConnectToServer_HostName()
        {
            yield return CanConnectToServer(localHost);

            Assert.IsTrue(success, "Connection closed early");
        }
        IEnumerator CanConnectToServer(string hostName)
        {
            // good address
            UriBuilder uriBuilder2 = new UriBuilder { Scheme = "tcp4", Host = hostName, Port = goodPort };
            manager.StartClient(uriBuilder2.Uri);

            float startTime = Time.time;
            success = false;
            const float waitTime = 1;
            while (NetworkClient.active)
            {
                //Debug.Log($"Connected = {clientTransport.ClientConnected()}");
                yield return null;

                // stop after x seconds
                if (Time.time > startTime + waitTime)
                {
                    success = true;
                    break;
                }
            }
        }


        [UnityTest]
        public IEnumerator StopEarlyShouldKillThread_Ip()
        {
            yield return StopEarlyShouldKillThread(localHostIp);

            Assert.IsTrue(success, "Connection closed early");
        }
        [UnityTest]
        public IEnumerator StopEarlyShouldKillThread_HostName()
        {
            yield return StopEarlyShouldKillThread(localHost);

            Assert.IsTrue(success, "Connection closed early");
        }

        IEnumerator StopEarlyShouldKillThread(string hostName)
        {
            // bad address
            UriBuilder uriBuilder1 = new UriBuilder { Scheme = "tcp4", Host = hostName, Port = badPort };
            manager.StartClient(uriBuilder1.Uri);

            float startTime = Time.time;
            const float waitTime1 = 0.2f;
            while (NetworkClient.active)
            {
                yield return null;

                // stop after x seconds
                // before timeout
                if (Time.time > startTime + waitTime1)
                {
                    break;
                }
            }

            // stop client and connect to another server
            manager.StopClient();

            // good address
            UriBuilder uriBuilder2 = new UriBuilder { Scheme = "tcp4", Host = hostName, Port = goodPort };
            manager.StartClient(uriBuilder2.Uri);

            // check to make sure connection stays open for waitTime2
            // 20 seconds should be long enough for the first connection to fail and close
            startTime = Time.time;
            success = false;
            const float waitTime2 = 20;
            while (NetworkClient.active)
            {
                yield return null;

                // stop after x seconds
                if (Time.time > startTime + waitTime2)
                {
                    success = true;
                    break;
                }
            }
        }

        [Test]
        public void Call2Times_Should_Give_Warning()
        {
            UriBuilder uriBuilder1 = new UriBuilder { Scheme = "tcp4", Host = localHost, Port = goodPort };
            manager.StartClient(uriBuilder1.Uri);

            UriBuilder uriBuilder2 = new UriBuilder { Scheme = "tcp4", Host = localHost, Port = goodPort };
            LogAssert.Expect(LogType.Warning, "Telepathy Client can not create connection because an existing connection is connecting or connected");
            manager.StartClient(uriBuilder2.Uri);
        }
    }
}
