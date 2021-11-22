using Mirror;

namespace WeaverSyncListTests.SyncListInheritance
{
    class SyncListInheritance : NetworkBehaviour
    {
        readonly SuperSyncList<string> superSyncListString = new SuperSyncList<string>();


        public class SuperSyncList<T> : SyncList<T>
        {

        }
    }
}
