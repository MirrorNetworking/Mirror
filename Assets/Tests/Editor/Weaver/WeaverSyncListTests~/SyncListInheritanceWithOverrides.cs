using Mirror;
using UnityEngine;

namespace WeaverSyncListTests.SyncListInheritanceWithOverrides
{
    class SyncListInheritanceWithOverrides : NetworkBehaviour
    {
        readonly SomeExtraList superSyncListString = new SomeExtraList();
    }

    // Type that cant have custom writer
    public class MyBehaviourWithValue : NetworkBehaviour
    {
        public Vector3 target;
    }

    public class SomeBaseList : SyncList<string>
    {
        protected override void SerializeItem(NetworkWriter writer, string item)
        {
            writer.WriteString(item);
        }
        protected override string DeserializeItem(NetworkReader reader)
        {
            return reader.ReadString();
        }
    }

    // Sync List type is MyBehaviourWithValue
    // MyBehaviourWithValue is an invalid type, so requires custom writers
    // Custom writers exist in base class so SomeExtraList should work without errors
    public class SomeExtraList : SomeBaseList
    {
        // do extra stuff here
    }
}
