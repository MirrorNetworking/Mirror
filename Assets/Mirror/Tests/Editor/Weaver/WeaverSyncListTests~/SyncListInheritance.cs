using Mirror;

namespace SyncListInheritance
{
    class MyBehaviour : NetworkBehaviour
    {
        readonly SuperSyncListString superSyncListString = new SuperSyncListString();
    }

    public class SuperSyncListString : SyncListString
    {

    }
}
