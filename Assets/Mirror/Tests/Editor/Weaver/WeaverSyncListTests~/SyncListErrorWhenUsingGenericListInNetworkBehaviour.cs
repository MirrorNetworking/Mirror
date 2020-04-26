using Mirror;

namespace Mirror.Weaver.Tests.SyncListErrorWhenUsingGenericListInNetworkBehaviour
{
    class SyncListErrorWhenUsingGenericListInNetworkBehaviour : NetworkBehaviour
    {
        readonly SomeList<int> someList;
    

        public class SomeList<T> : SyncList<T> { }
    }
}
