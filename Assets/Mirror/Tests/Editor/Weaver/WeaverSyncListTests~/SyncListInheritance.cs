using Mirror;

namespace WeaverSyncListTests.SyncListInheritance
{
    class SyncListInheritance : NetworkBehaviour
    {
        readonly SuperSyncListString superSyncListString = new SuperSyncListString();


        public class SuperSyncListString : SyncListString
        {

        }
    }
}
