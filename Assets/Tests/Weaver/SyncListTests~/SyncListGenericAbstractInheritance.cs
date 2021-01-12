using Mirror;

namespace SyncListTests.SyncListGenericAbstractInheritance
{
    class SyncListGenericAbstractInheritance : NetworkBehaviour
    {
        readonly SomeListInt superSyncListString = new SomeListInt();


        public abstract class SomeList<T> : SyncList<T> { }

        public class SomeListInt : SomeList<int> { }
    }
}
