using Mirror;

namespace SyncListTests.SyncListErrorWhenUsingGenericListInNetworkBehaviour
{
    class SyncListErrorWhenUsingGenericListInNetworkBehaviour : NetworkBehaviour
    {
        readonly SomeList<int> someList;


        public class SomeList<T> : SyncList<T> { }
    }
}
