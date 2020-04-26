using Mirror;

namespace SyncListErrorForGenericInheritanceWithMultipleGeneric
{
    /*
    This test will fail
    It is hard to know which generic argument we want from `SomeList<string, int>`
    So instead give a useful error for this edge case
    */

    class MyBehaviour : NetworkBehaviour
    {
        readonly SomeListInt someList = new SomeListInt();
    }

    public class SomeList<G, T> : SyncList<T> { }

    public class SomeListInt : SomeList<string, int> { }
}
