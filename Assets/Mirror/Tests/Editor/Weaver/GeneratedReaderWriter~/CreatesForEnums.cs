using Mirror;

namespace MirrorTest
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
