using Mirror;

namespace WeaverSyncListTests.SyncListGenericInheritanceWithMultipleGeneric
{
    /*
    This test should pass
    */
    class SyncListGenericInheritanceWithMultipleGeneric : NetworkBehaviour
    {
        readonly SomeListInt someList = new SomeListInt();


        public class SomeList<G, T> : SyncList<T> { }

        public class SomeListInt : SomeList<string, int> { }
    }
}
