using Mirror;
using Mirror.Weaver.Tests.Extra;

namespace GeneratedReaderWriter.ExcludesNonSerializedFields
{
    public class ExcludesNonSerializedFields : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(SomeDataUsingNonSerialized data)
        {
            // empty
        }
    }

    public struct SomeDataUsingNonSerialized
    {
        public int usefulNumber;
        // Object is a not allowed type
        [System.NonSerialized] public UnityEngine.Object obj;
    }
}
