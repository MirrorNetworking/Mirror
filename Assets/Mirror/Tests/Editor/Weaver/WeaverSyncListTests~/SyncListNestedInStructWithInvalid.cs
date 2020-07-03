using Mirror;
using UnityEngine;

namespace WeaverSyncListTests.SyncListNestedInStructWithInvalid
{
    class SyncListNestedInStructWithInvalid : NetworkBehaviour
    {
        SomeData.SyncList Foo;


        public struct SomeData
        {
            public int usefulNumber;
            public Object target;

            public class SyncList : Mirror.SyncList<SomeData> { }
        }
    }
}
