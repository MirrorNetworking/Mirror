using Mirror;

namespace WeaverSyncSetTests.SyncSetGenericAbstractInheritance
{
    class SyncSetGenericAbstractInheritance : NetworkBehaviour
    {
        readonly SomeSetInt superSyncSetString = new SomeSetInt();

        public abstract class SomeSet<T> : SyncHashSet<T> { }

        public class SomeSetInt : SomeSet<int> { }
    }
}
