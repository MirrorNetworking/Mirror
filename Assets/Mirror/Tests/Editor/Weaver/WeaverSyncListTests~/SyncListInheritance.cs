using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncListInheritance
{
    class SyncListInheritance : NetworkBehaviour
    {
        readonly SuperSyncListString superSyncListString = new SuperSyncListString();
    
        
        public class SuperSyncListString : SyncListString
        {

        }
    }
}
