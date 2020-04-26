// this class generates OnSerialize/OnDeserialize for SyncLists
using Mono.CecilX;

namespace Mirror.Weaver
{
    static class SyncDictionaryProcessor
    {
        /// <summary>
        /// Generates serialization methods for synclists
        /// </summary>
        /// <param name="td">The synclist class</param>
        public static void Process(TypeDefinition td)
        {
            GenericArgumentResolver resolver = new GenericArgumentResolver(2);

            if (resolver.GetGenericFromBaseClass(td, 0, Weaver.SyncDictionaryType, out TypeReference keyType))
            {
                SyncObjectProcessor.GenerateSerialization(td, keyType, Weaver.SyncDictionaryType, "SerializeKey", "DeserializeKey");
            }
            else
            {
                Weaver.Error($"Could not find generic arguments for SyncDictionary in {td.Name}", td);
                return;
            }

            if (resolver.GetGenericFromBaseClass(td, 1, Weaver.SyncDictionaryType, out TypeReference itemType))
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
