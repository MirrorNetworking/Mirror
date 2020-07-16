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
            GenericArgumentResolver resolver = new GenericArgumentResolver(2);

            TypeReference keyType = resolver.GetGenericFromBaseClass(td, 0, Weaver.SyncDictionaryType);
            if (keyType != null)
            {
                SyncObjectProcessor.GenerateSerialization(td, keyType, Weaver.SyncDictionaryType, "SerializeKey", "DeserializeKey");
            }
            else
            {
                Weaver.Error($"Could not find generic arguments for SyncDictionary in {td.Name}", td);
                return;
            }

            TypeReference itemType = resolver.GetGenericFromBaseClass(td, 1, Weaver.SyncDictionaryType);
            if (itemType != null)
            {
                SyncObjectProcessor.GenerateSerialization(td, itemType, Weaver.SyncDictionaryType, "SerializeItem", "DeserializeItem");
            }
            else
            {
                Weaver.Error($"Could not find generic arguments for SyncDictionary in {td.Name}", td);
            }
        }
    }
}
