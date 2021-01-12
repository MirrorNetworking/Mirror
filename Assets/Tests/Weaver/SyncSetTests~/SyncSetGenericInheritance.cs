using Mirror;

namespace SyncSetTests.SyncSetGenericInheritance
{
    class SyncSetGenericInheritance : NetworkBehaviour
    {
        readonly SomeSetInt someSet = new SomeSetInt();


        public class SomeSet<T> : SyncHashSet<T> { }

        public class SomeSetInt : SomeSet<int> { }
    }
}
