using Mirror;

namespace WeaverSyncVarTests.SyncVarsInterface
{
    class SyncVarsInterface : NetworkBehaviour
    {
        interface IMySyncVar
        {
            void interfaceMethod();
        }
        [SyncVar]
        IMySyncVar invalidVar;
    }
}
