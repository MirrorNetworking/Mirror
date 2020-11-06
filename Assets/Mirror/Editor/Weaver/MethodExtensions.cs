using Mono.Cecil;

namespace Mirror.Weaver
{
    /// <summary>
    /// Convenient extensions for modifying methods
    /// </summary>
    public static class MethodExtensions
    {
        public static ParameterDefinition AddParam<T>(this MethodDefinition method, string name, ParameterAttributes attributes = ParameterAttributes.None)
        {
            var param = new ParameterDefinition(name, attributes, WeaverTypes.Import<T>());
            method.Parameters.Add(param);
            return param;
        }

        public static ParameterDefinition AddParam(this MethodDefinition method, TypeReference typeRef, string name, ParameterAttributes attributes = ParameterAttributes.None)
        {
            var param = new ParameterDefinition(name, attributes, typeRef);
            method.Parameters.Add(param);
            return param;
        }
    }
}