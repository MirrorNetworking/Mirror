using Mirror;

namespace SyncListErrorWhenUsingGenericListInNetworkBehaviour
{
    class MyBehaviour : NetworkBehaviour
    {
        readonly SomeList<int> someList;
    }

    public class SomeList<T> : SyncList<T> { }
}
