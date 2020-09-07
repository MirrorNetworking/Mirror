using Mirror;
using UnityEngine;

namespace WeaverSyncListTests.SyncListNestedInAbstractClassWithInvalid
{
    class SyncListNestedStructWithInvalid : NetworkBehaviour
    {
        SomeAbstractClass.MyNestedStructList Foo;


        public abstract class SomeAbstractClass
        {
            public struct MyNestedStruct
            {
                public int potato;
                public Object target;
            }
            public class MyNestedStructList : SyncList<MyNestedStruct> { }
        }
    }
}
