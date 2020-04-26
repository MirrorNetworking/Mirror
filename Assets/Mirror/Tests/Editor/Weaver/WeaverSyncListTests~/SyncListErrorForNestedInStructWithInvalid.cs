using Mirror;
using UnityEngine;

namespace SyncListErrorForNestedInStructWithInvalid
{
    class MyBehaviour : NetworkBehaviour
    {
        SomeData.SyncList Foo;


    }

    public struct SomeData
    {
        public int usefulNumber;
        public Object target;

        public class SyncList : Mirror.SyncList<SomeData> { }
    }
}
