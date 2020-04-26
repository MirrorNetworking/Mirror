using Mirror;

namespace SyncListGenericAbstractInheritance
{
    class MyBehaviour : NetworkBehaviour
    {
        readonly SomeListInt superSyncListString = new SomeListInt();
    }

    public abstract class SomeList<T> : SyncList<T> { }

    public class SomeListInt : SomeList<int> { }
}
