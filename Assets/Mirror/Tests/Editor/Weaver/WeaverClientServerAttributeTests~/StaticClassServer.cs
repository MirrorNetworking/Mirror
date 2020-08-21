using Mirror;

namespace WeaverClientServerAttributeTests.StaticClassServer
{
    static class StaticClassServer
    {
        [Server]
        static void ServerOnlyMethod() { }
    }
}
