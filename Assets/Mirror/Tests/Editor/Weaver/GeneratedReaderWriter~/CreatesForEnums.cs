using Mirror;

namespace Mirror.Weaver.Tests.CreatesForEnums
{
    class CreatesForEnums : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(FunEnum data)
        {
            // empty
        }
    }

    public enum FunEnum
    {
        A,
        B,
        C,
        D,
    }
}
