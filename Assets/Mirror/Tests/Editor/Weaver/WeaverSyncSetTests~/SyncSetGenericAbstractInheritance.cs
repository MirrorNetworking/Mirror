using Mirror;

namespace Mirror.Weaver.Tests.SyncSetGenericAbstractInheritance
{
    class SyncSetGenericAbstractInheritance : NetworkBehaviour
    {
        readonly SomeSetInt superSyncSetString = new SomeSetInt();
    

        public abstract class SomeSet<T> : SyncHashSet<T> { }

        public class SomeSetInt : SomeSet<int> { }
    }
}
