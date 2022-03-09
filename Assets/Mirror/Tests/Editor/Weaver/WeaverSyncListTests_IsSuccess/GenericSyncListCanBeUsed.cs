using Mirror;

namespace WeaverSyncListTests.GenericSyncListCanBeUsed
{
    class GenericSyncListCanBeUsed : NetworkBehaviour
    {
        readonly SomeList<int> someList;


        public class SomeList<T> : SyncList<T> { }
    }
}
