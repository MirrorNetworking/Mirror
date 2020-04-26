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
        /// <param name="mirrorBaseType">the base SyncObject td inherits from</param>
        public static void Process(TypeDefinition td, TypeReference mirrorBaseType)
        {
            GenericArgumentResolver resolver = new GenericArgumentResolver(1);

            if (resolver.GetGenericFromBaseClass(td, 0, mirrorBaseType, out TypeReference itemType))
            {
                SyncObjectProcessor.GenerateSerialization(td, itemType, mirrorBaseType, "SerializeItem", "DeserializeItem");
            }
            else
            {
                Weaver.Error($"Could not find generic arguments for {mirrorBaseType.Name} in {td}", td);
            }
        }
    }
}
