using Mirror;

namespace WeaverSyncListTests.SyncListErrorWhenUsingGenericListInNetworkBehaviour
{
    class SyncListErrorWhenUsingGenericListInNetworkBehaviour : NetworkBehaviour
    {
        readonly SomeList<int> someList;


        public class SomeList<T> : SyncList<T> { }
    }
}
