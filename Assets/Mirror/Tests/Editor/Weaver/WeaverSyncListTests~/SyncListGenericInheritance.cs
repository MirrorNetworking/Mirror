using Mirror;

namespace SyncListGenericInheritance
{
    class MyBehaviour : NetworkBehaviour
    {
        readonly SomeListInt someList = new SomeListInt();
    }

    public class SomeList<T> : SyncList<T> { }

    public class SomeListInt : SomeList<int> { }
}
