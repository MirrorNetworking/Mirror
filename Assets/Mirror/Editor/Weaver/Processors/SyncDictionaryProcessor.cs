using Mono.Cecil;

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
            var resolver = new GenericArgumentResolver(2);
            TypeReference syncDictionaryType = WeaverTypes.Import(typeof(SyncDictionary<,>));
            TypeReference keyType = resolver.GetGenericFromBaseClass(td, 0, syncDictionaryType);
            if (keyType != null)
            {
                SyncObjectProcessor.GenerateSerialization(td, keyType, syncDictionaryType, "SerializeKey", "DeserializeKey");
            }
            else
            {
                Weaver.Error($"Could not find generic arguments for SyncDictionary in {td.Name}", td);
                return;
            }

            TypeReference itemType = resolver.GetGenericFromBaseClass(td, 1, syncDictionaryType);
            if (itemType != null)
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
