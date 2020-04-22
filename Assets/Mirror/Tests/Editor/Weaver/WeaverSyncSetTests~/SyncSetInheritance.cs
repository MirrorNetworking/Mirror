using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncSetInheritance : NetworkBehaviour
    {
        readonly SuperSet superSet = new SuperSet();
    }
    
    public class SomeSet : SyncHashSet<string> { }

    public class SuperSet : SomeSet { }
}
