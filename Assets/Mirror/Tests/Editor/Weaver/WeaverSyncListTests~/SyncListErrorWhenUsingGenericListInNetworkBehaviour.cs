using Mirror;

namespace MirrorTest
{
    class SyncListErrorWhenUsingGenericListInNetworkBehaviour : NetworkBehaviour
    {
        readonly SomeList<int> someList;
    }

    public class SomeList<T> : SyncList<T> { }
}
