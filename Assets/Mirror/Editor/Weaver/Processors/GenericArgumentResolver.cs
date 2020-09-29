using System;
using Mono.CecilX;

namespace Mirror.Weaver
{
    public static class GenericArgumentResolver
    {
        static TypeReference[] GetGenericArguments(TypeReference tr, Type baseType, TypeReference[] genericArguments)
        {
            if (tr == null)
                return null;

            TypeReference[] resolvedArguments = new TypeReference[0];

            if (tr is GenericInstanceType genericInstance)
            {
                // this type is a generic instance,  for example List<int>
                // however, the parameter may be generic itself,  for example:
                // List<T>.  If T is a generic parameter,  then look it up in
                // from the arguments we got.
                resolvedArguments = genericInstance.GenericArguments.ToArray();

                for (int i = 0; i< resolvedArguments.Length; i++)
                {
                    TypeReference argument = resolvedArguments[i];
                    if (argument is GenericParameter genericArgument)
                    {
                        resolvedArguments[i] = genericArguments[genericArgument.Position];
                    }
                }
            }

            if (tr.Is(baseType))
                return resolvedArguments;

            if (tr.CanBeResolved())
                return GetGenericArguments(tr.Resolve().BaseType, baseType, resolvedArguments);

            return null;
        }

        /// <summary>
        /// Find out what the arguments are to a generic base type
        /// </summary>
        /// <param name="td"></param>
        /// <param name="baseType"></param>
        /// <returns></returns>
        public static TypeReference[] GetGenericArguments(TypeDefinition td, Type baseType)
        {
            if (td == null)
                return null;

            return GetGenericArguments(td.BaseType, baseType, new TypeReference[] { });
        }
    }
}
