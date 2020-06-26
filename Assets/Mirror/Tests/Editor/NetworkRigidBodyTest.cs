using Mirror.Experimental;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkRigidBodyTests
{
    public abstract class NetworkRigidbodyTestBase
    {
        public NetworkRigidbody clientBody;
        public NetworkRigidbody serverBody;

        [SetUp]
        public void Setup()
        {
            clientBody = CreateNetworkRigidBody(false, false);
            serverBody = CreateNetworkRigidBody(true, false);

            RunUpdate(true);
        }

        NetworkRigidbody CreateNetworkRigidBody(bool isServer, bool hasAuthority)
        {
            GameObject go = new GameObject();
            NetworkIdentity netId = go.AddComponent<NetworkIdentity>();
            netId.isServer = isServer;
            netId.isClient = !isServer;
            netId.hasAuthority = hasAuthority;
            go.AddComponent<Rigidbody>();
            NetworkRigidbody netBody = go.AddComponent<NetworkRigidbody>();
            return netBody;
        }

        [TearDown]
        public void TearDown()
        {
            if (clientBody != null)
            {
                Object.DestroyImmediate(clientBody);
            }
            if (serverBody != null)
            {
                Object.DestroyImmediate(serverBody);
            }
        }

        public void RunUpdate(bool initialState = false)
        {
            serverBody.FixedUpdate();
            clientBody.FixedUpdate();

            serverBody.Update();
            clientBody.Update();

            // Late Update
            NetworkWriter writer = new NetworkWriter();
            serverBody.OnSerialize(writer, initialState);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            clientBody.OnDeserialize(reader, initialState);

            int writeLength = writer.Length;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerialize and OnDeserialize doesn't read write the same amount of bytes\n    writeLength={writeLength}\n    readLength={readLength}");
        }
    }

    public class NetworkRigidbodyServerAuth : NetworkRigidbodyTestBase
    {
        [Test]
        public void UpdatingIsKinematicSyncsValueToClient()
        {
            // start false
            clientBody.target.isKinematic = false;
            serverBody.target.isKinematic = true;

            RunUpdate();

            Assert.IsTrue(clientBody.target.isKinematic);
        }
    }

    public class NetworkRigidbodyClientAuth : NetworkRigidbodyTestBase
    {

    }
}
