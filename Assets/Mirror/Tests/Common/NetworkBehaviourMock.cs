// simple network behaviour mock component which counts calls.
// this is necessary for many tests.
using UnityEngine;

namespace Mirror.Tests
{
    [AddComponentMenu("")]
    public class NetworkBehaviourMock : NetworkBehaviour
    {
        // start
        public int onStartClientCalled;
        public override void OnStartClient() => ++onStartClientCalled;

        public int onStartLocalPlayerCalled;
        public override void OnStartLocalPlayer() => ++onStartLocalPlayerCalled;

        public int onStartServerCalled;
        public override void OnStartServer() => ++onStartServerCalled;

        public int onStartAuthorityCalled;
        public override void OnStartAuthority() => ++onStartAuthorityCalled;

        // stop
        public int onStopClientCalled;
        public override void OnStopClient() => ++onStopClientCalled;

        public int onStopLocalPlayerCalled;
        public override void OnStopLocalPlayer() => ++onStopLocalPlayerCalled;

        public int onStopServerCalled;
        public override void OnStopServer() => ++onStopServerCalled;

        public int onStopAuthorityCalled;
        public override void OnStopAuthority() => ++onStopAuthorityCalled;
    }
}
