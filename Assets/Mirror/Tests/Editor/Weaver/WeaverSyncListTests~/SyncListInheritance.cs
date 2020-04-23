using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncListInheritance : NetworkBehaviour
    {
        readonly SuperSyncListString superSyncListString = new SuperSyncListString();
    
        
        public class SuperSyncListString : SyncListString
        {

        }
    }
}
