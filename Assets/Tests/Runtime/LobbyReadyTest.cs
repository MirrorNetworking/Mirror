using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    [TestFixture]
    public class LobbyReadyTest : HostSetup<MockComponent>
    {
        GameObject readyPlayer;
        LobbyReady lobby;
        ObjectReady readyComp;

        public override void ExtraSetup()
        {
            lobby = networkManagerGo.AddComponent<LobbyReady>();
        }

        public override void ExtraTearDown()
        {
            lobby = null;
        }

        [Test]
        public void SetAllClientsNotReadyTest()
        {
            readyComp = identity.gameObject.AddComponent<ObjectReady>();
            lobby.ObjectReadyList.Add(readyComp);
            readyComp.IsReady = true;

            lobby.SetAllClientsNotReady();

            Assert.That(readyComp.IsReady, Is.False);
        }

        [UnityTest]
        public IEnumerator SendToReadyTest() => RunAsync(async () =>
        {
            readyComp = identity.gameObject.AddComponent<ObjectReady>();
            lobby.ObjectReadyList.Add(readyComp);
            readyComp.IsReady = true;

            bool invokeWovenTestMessage = false;
            client.Connection.RegisterHandler<SceneMessage>(msg => invokeWovenTestMessage = true);
            lobby.SendToReady(identity, new SceneMessage(), true, Channels.DefaultReliable);

            await WaitFor(() => invokeWovenTestMessage);
        });

        [Test]
        public void IsReadyStateTest()
        {
            readyComp = identity.gameObject.AddComponent<ObjectReady>();

            Assert.That(readyComp.IsReady, Is.False);
        }

        [Test]
        public void SetClientReadyTest()
        {
            readyComp = identity.gameObject.AddComponent<ObjectReady>();

            readyComp.SetClientReady();

            Assert.That(readyComp.IsReady, Is.True);
        }

        [Test]
        public void SetClientNotReadyTest()
        {
            readyComp = identity.gameObject.AddComponent<ObjectReady>();

            readyComp.SetClientNotReady();

            Assert.That(readyComp.IsReady, Is.False);
        }

        [Test]
        public void ClientReadyTest()
        {
            readyPlayer = new GameObject();
            readyPlayer.AddComponent<NetworkIdentity>();
            readyComp = readyPlayer.AddComponent<ObjectReady>();

            server.Spawn(readyPlayer, server.LocalConnection);
            readyComp.Ready();

            Assert.That(readyComp.IsReady, Is.False);
        }
    }
}
