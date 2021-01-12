using Mirror;

namespace SyncVarTests.SyncVarsDerivedNetworkBehaviour
{
    class MyBehaviour : NetworkBehaviour
    {
        public int abc = 123;
    }
    class SyncVarsDerivedNetworkBehaviour : NetworkBehaviour
    {
        [SyncVar]
        MyBehaviour invalidVar;
    }
}
