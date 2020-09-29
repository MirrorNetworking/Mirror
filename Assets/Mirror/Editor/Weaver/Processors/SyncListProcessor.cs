using System;
using Mono.CecilX;

namespace Mirror.Weaver
{
    /// <summary>
    /// generates OnSerialize/OnDeserialize for SyncLists
    /// </summary>
    static class SyncListProcessor
    {
        /// <summary>
        /// Generates serialization methods for synclists
        /// </summary>
        /// <param name="td">The synclist class</param>
        /// <param name="mirrorBaseType">the base SyncObject td inherits from</param>
        public static void Process(TypeDefinition td, Type mirrorBaseType)
        {
            TypeReference [] arguments = GenericArgumentResolver.GetGenericArguments(td, mirrorBaseType);
            if (arguments != null)
            {
                SyncObjectProcessor.GenerateSerialization(td, arguments[0], mirrorBaseType, "SerializeItem", "DeserializeItem");
            }
            else
            {
                Weaver.Error($"Could not find generic arguments for {mirrorBaseType.Name} in {td}", td);
            }
        }
    }
}
