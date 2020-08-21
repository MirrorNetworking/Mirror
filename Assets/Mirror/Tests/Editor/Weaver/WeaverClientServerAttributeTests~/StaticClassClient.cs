using Mirror;

namespace WeaverClientServerAttributeTests.StaticClassClient
{
    static class StaticClassClient
    {
        [Client]
        static void ClientOnlyMethod() { }
    }
}
