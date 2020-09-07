using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class NetworkBehaviourEvents : NetworkBehaviour
    {
        public event Action onStartLocalPlayer;
        public event Action onStartAuthority;
        public event Action onStopAuthority;
        public event Action onStartClient;
        public event Action onStopClient;
        public event Action onStartServer;
        public event Action onStopServer;

        public override void OnStartLocalPlayer()
        {
            onStartLocalPlayer?.Invoke();
        }

        public override void OnStartAuthority()
        {
            onStartAuthority?.Invoke();
        }
        public override void OnStopAuthority()
        {
            onStopAuthority?.Invoke();
        }

        public override void OnStartClient()
        {
            onStartClient?.Invoke();
        }
        public override void OnStopClient()
        {
            onStopClient?.Invoke();
        }

        public override void OnStartServer()
        {
            onStartServer?.Invoke();
        }
        public override void OnStopServer()
        {
            onStopServer?.Invoke();
        }
    }
    public class NetworkBehaviourCallbacks : HostSetup
    {
        [UnityTest]
        public IEnumerator OnStopClientIsCalledWhenNetworkIdetityIsDestroyed()
        {
            GameObject go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            NetworkBehaviourEvents events = go.AddComponent<NetworkBehaviourEvents>();
            identity.assetId = new System.Guid();

            int onStartClientCalled = 0;
            events.onStartClient += () => onStartClientCalled++;
            int onStopClientCalled = 0;
            events.onStopClient += () => onStopClientCalled++;


            NetworkServer.Spawn(go);

            // wait 1 frame for messages
            yield return null;

            // start should be called
            Assert.That(onStartClientCalled, Is.EqualTo(1));


            NetworkServer.Destroy(go);

            // wait 1 frame for messages
            yield return null;

            // stop should have been called
            Assert.That(onStopClientCalled, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OnStopClientIsCalledWhenExitingPlayerMode()
        {
            yield return new EnterPlayMode();

            GameObject go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            NetworkBehaviourEvents events = go.AddComponent<NetworkBehaviourEvents>();
            identity.assetId = new System.Guid();

            int onStopClientCalled = 0;
            events.onStopClient += () => onStopClientCalled++;

            //spawn
            NetworkServer.Spawn(go);

            // stop play mopde
            yield return new ExitPlayMode();

            // stop should have been called
            Assert.That(onStopClientCalled, Is.EqualTo(1));
        }
    }
}
