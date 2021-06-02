using NUnit.Framework;

namespace Mirror.Tests.RemoteAttrributeTest
{
    public class RemoteTestBase : MirrorEditModeTest
    {
        [SetUp]
        public void Setup()
        {
            // start server/client
            NetworkServer.Listen(1);
            NetworkClient.ConnectHost();
            NetworkServer.SpawnObjects();
            NetworkServer.ActivateHostScene();
            NetworkClient.ConnectLocalServer();
            NetworkServer.localConnection.isAuthenticated = true;
            NetworkClient.connection.isAuthenticated = true;
            NetworkClient.Ready();
        }

        [TearDown]
        public override void TearDown()
        {
            // stop server/client
            NetworkClient.Disconnect();
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();
            NetworkIdentity.spawned.Clear();
            base.TearDown();
        }
    }
}
