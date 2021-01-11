using Mirror;

namespace WeaverClientServerAttributeTests.NetworkBehaviourClient
{
    class NetworkBehaviourClient : NetworkBehaviour
    {
        [Client]
        void ClientOnlyMethod()
        {
            // test method
        }

        [Client]
        void ClientMethodWithParam(int pepe)
        {
        }

        [Client]
        void ClientMethodWithOutPrimitiveParam(out int pepe)
        {
            pepe = 10;
        }

        [Client]
        void ClientMethodWithOutObjectParam(out object pepe)
        {
            pepe = new object();
        }

        [Client]
        int ClientMethodWithPrimitiveReturnValue()
        {
            return 10;
        }

        [Client]
        object clientMethodWithObjectReturnValue()
        {
            return new object();
        }
    }
}
