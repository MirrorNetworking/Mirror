using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncListNestedInAbstractClass
{
    class SyncListNestedStruct : NetworkBehaviour
    {
        SomeAbstractClass.MyNestedStructList Foo;
    

        public abstract class SomeAbstractClass
        {
            public struct MyNestedStruct
            {
                public int potato;
                public float floatingpotato;
                public double givemetwopotatoes;
            }
            public class MyNestedStructList : SyncList<MyNestedStruct> { }
        }
    }
}
