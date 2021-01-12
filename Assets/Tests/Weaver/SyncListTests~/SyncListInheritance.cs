using Mirror;

namespace SyncListTests.SyncListInheritance
{
    class SyncListInheritance : NetworkBehaviour
    {
        readonly SuperSyncListString superSyncListString = new SuperSyncListString();


        public class SuperSyncListString : SyncList<string>
        {

        }
    }
}
