using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    // proof of concept for a return value from Command/Rpc
    public class ReturnValue<T> : CustomYieldInstruction
    {
        public T value { get; private set; }
        public bool Complete { get; private set; }

        public override bool keepWaiting => !Complete;

        internal void NetworkResponse(T value)
        {
            this.value = value;
            Complete = true;
        }

        public ReturnValue()
        {
        }
        public ReturnValue(T value)
        {
            this.value = value;
        }

        public static implicit operator ReturnValue<T>(T value)
        {
            return new ReturnValue<T>(value);
        }
    }
    public class CommandReturnBehaviour : NetworkBehaviour
    {
        public int serverInt;

        public override void OnStartServer()
        {
            serverInt = UnityEngine.Random.Range(1, 100);
        }

        // [Command] ... can't really be command yet because weaver needs to be changed
        public ReturnValue<int> GetServerInt()
        {
            waitValue = new ReturnValue<int>();
            CmdGetServerInt();
            return waitValue;
        }


        private ReturnValue<int> waitValue;
        [Command(ignoreAuthority = true)]
        private void CmdGetServerInt(NetworkConnectionToClient sender = null)
        {
            TargetGetServerInt(sender, serverInt);
        }
        [TargetRpc]
        private void TargetGetServerInt(NetworkConnection conn, int value)
        {
            waitValue.NetworkResponse(value);
        }
    }



    public class CommandReturnTest : HostSetup
    {
        [UnityTest, Timeout(1000)]
        public IEnumerator GetReturnValue()
        {
            // setup
            GameObject go = new GameObject();
            NetworkIdentity id = go.AddComponent<NetworkIdentity>();
            CommandReturnBehaviour behaviour = go.AddComponent<CommandReturnBehaviour>();

            // spawn object
            NetworkServer.Spawn(go);

            // wait
            yield return null;
            int serverInt = behaviour.serverInt;
            // server int should have been set to random non-zero value
            Assert.That(serverInt, Is.Not.Zero);

            ReturnValue<int> result = behaviour.GetServerInt();

            Assert.That(result.Complete, Is.False);
            while (result.keepWaiting)
            {
                //yield return new WaitForSeconds(0.1f);
                yield return null;
            }
            Assert.That(result.Complete, Is.True);

            Assert.That(result.value, Is.EqualTo(serverInt));

            // clean up
            NetworkServer.Shutdown();
            NetworkClient.Shutdown();

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
