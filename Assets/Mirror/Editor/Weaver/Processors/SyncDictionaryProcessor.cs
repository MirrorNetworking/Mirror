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
            GenericArgumentResolver resolver = new GenericArgumentResolver(2);
            TypeReference syncDictionaryType = WeaverTypes.Import(typeof(SyncDictionary<,>));

            if (resolver.GetGenericFromBaseClass(td, 0, syncDictionaryType, out TypeReference keyType))
            {
                SyncObjectProcessor.GenerateSerialization(td, keyType, syncDictionaryType, "SerializeKey", "DeserializeKey");
            }
            else
            {
                Weaver.Error($"Could not find generic arguments for SyncDictionary in {td.Name}", td);
                return;
            }

            if (resolver.GetGenericFromBaseClass(td, 1, syncDictionaryType, out TypeReference itemType))
            {
                SyncObjectProcessor.GenerateSerialization(td, itemType, syncDictionaryType, "SerializeItem", "DeserializeItem");
            }
            else
            {
                Weaver.Error($"Could not find generic arguments for SyncDictionary in {td.Name}", td);
            }
        }
    }
}
