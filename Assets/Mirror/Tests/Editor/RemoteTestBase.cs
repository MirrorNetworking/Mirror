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
            ConnectHostClientBlockingAuthenticatedAndReady();
        }
    }
}
