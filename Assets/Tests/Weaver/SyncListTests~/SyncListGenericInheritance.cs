using Mirror;

namespace SyncListTests.SyncListGenericInheritance
{
    class SyncListGenericInheritance : NetworkBehaviour
    {
        readonly SomeListInt someList = new SomeListInt();


        public class SomeList<T> : SyncList<T> { }

        public class SomeListInt : SomeList<int> { }
    }
}
