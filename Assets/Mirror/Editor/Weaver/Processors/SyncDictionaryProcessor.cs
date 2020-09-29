using Mono.CecilX;

namespace Mirror.Weaver
{
    /// <summary>
    /// generates OnSerialize/OnDeserialize for SyncLists
    /// </summary>
    static class SyncDictionaryProcessor
    {
        /// <summary>
        /// Generates serialization methods for synclists
        /// </summary>
        /// <param name="td">The synclist class</param>
        public static void Process(TypeDefinition td)
        {
            TypeReference []arguments = GenericArgumentResolver.GetGenericArguments(td, typeof(SyncDictionary<,>));
            if (arguments != null)
            {
                SyncObjectProcessor.GenerateSerialization(td, arguments[0], typeof(SyncDictionary<,>), "SerializeKey", "DeserializeKey");
                SyncObjectProcessor.GenerateSerialization(td, arguments[1], typeof(SyncDictionary<,>), "SerializeItem", "DeserializeItem");
            }
            else
            {
                Weaver.Error($"Could not find generic arguments for SyncDictionary in {td.Name}", td);
            }
        }
    }
}
