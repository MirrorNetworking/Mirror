using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.CommandAttrributeTest
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

    public class CommandTest
    {
        private List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void Setup()
        {
            Transport.activeTransport = new GameObject().AddComponent<MemoryTransport>();

            // start server/client
            NetworkServer.Listen(1);
            NetworkClient.ConnectHost();
            NetworkServer.SpawnObjects();
            NetworkServer.ActivateHostScene();
            NetworkClient.ConnectLocalServer();

            NetworkServer.localConnection.isAuthenticated = true;
            NetworkClient.connection.isAuthenticated = true;

            ClientScene.Ready(NetworkClient.connection);
        }

        [TearDown]
        public void TearDown()
        {
            // stop server/client
            NetworkClient.DisconnectLocalServer();

            NetworkClient.Disconnect();
            NetworkClient.Shutdown();

            NetworkServer.Shutdown();

            // destroy left over objects
            foreach (GameObject item in spawned)
            {
                if (item != null)
                {
                    GameObject.DestroyImmediate(item);
                }
            }

            spawned.Clear();

            NetworkIdentity.spawned.Clear();

            GameObject.DestroyImmediate(Transport.activeTransport.gameObject);
        }


        T CreateHostObject<T>(bool spawnWithAuthority) where T : NetworkBehaviour
        {
            GameObject gameObject = new GameObject();
            spawned.Add(gameObject);

            gameObject.AddComponent<NetworkIdentity>();

            T behaviour = gameObject.AddComponent<T>();

            // spawn outwith authority
            if (spawnWithAuthority)
            {
                NetworkServer.Spawn(gameObject, NetworkServer.localConnection);
            }
            else
            {
                NetworkServer.Spawn(gameObject);
            }
            ProcessMessages();

            Debug.Assert(behaviour.hasAuthority == spawnWithAuthority, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");

            return behaviour;
        }

        private static void ProcessMessages()
        {
            // run update so message are processed
            NetworkServer.Update();
            NetworkClient.Update();
        }

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
