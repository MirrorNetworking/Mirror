// this class generates OnSerialize/OnDeserialize for SyncLists
using Mono.CecilX;

namespace Mirror.Weaver
{
    static class SyncListProcessor
    {
        /// <summary>
        /// Generates serialization methods for synclists
        /// </summary>
        /// <param name="td">The synclist class</param>
        public static void Process(TypeDefinition td)
        {
            SyncObjectProcessor.GenerateSerialization(td, 0, "SerializeItem", "DeserializeItem");
        }
    }
}
