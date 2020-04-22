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
        public static void Process(TypeDefinition td, TypeReference baseType)
        {
            GenericArgumentResolver resolver = new GenericArgumentResolver(1);

            if (resolver.GetGenericFromBaseClass(td, 0, baseType, out TypeReference itemType))
            {
                SyncObjectProcessor.GenerateSerialization(td, itemType, "SerializeItem", "DeserializeItem");
            }
            else
            {
                Weaver.Error($"Could not find generic arguments for {baseType} using {td}");
            }
        }
    }
}
