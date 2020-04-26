using System.Collections.Generic;
using Mono.Cecil;

namespace Mirror.Weaver
{
    public class GenericArgumentResolver
    {
        readonly Stack<TypeReference> stack = new Stack<TypeReference>();
        readonly int maxGenericArgument;

        public GenericArgumentResolver(int maxGenericArgument)
        {
            this.maxGenericArgument = maxGenericArgument;
        }

        public bool GetGenericFromBaseClass(TypeDefinition td, int genericArgument, TypeReference baseType, out TypeReference itemType)
        {
            itemType = null;
            if (GetGenericBaseType(td, baseType, out GenericInstanceType parent))
            {
                TypeReference arg = parent.GenericArguments[genericArgument];
                if (arg.IsGenericParameter)
                {
                    itemType = FindParameterInStack(td, genericArgument);
                }
                else
                {
                    itemType = Weaver.CurrentAssembly.MainModule.ImportReference(arg);
                }
            }

            return itemType != null;
        }

        TypeReference FindParameterInStack(TypeDefinition td, int genericArgument)
        {
            while (stack.Count > 0)
            {
                TypeReference next = stack.Pop();

                if (!(next is GenericInstanceType genericType))
                {
                    // if type is not GenericInstanceType something has gone wrong
                    return null;
                }

                if (genericType.GenericArguments.Count < genericArgument)
                {
                    // if less than `genericArgument` then we didnt find generic argument
                    return null;
                }

                if (genericType.GenericArguments.Count > maxGenericArgument)
                {
                    // if greater than `genericArgument` it is hard to know which generic arg we want
                    // See SyncListGenericInheritanceWithMultipleGeneric test
                    Weaver.Error($"Type {td.Name} has too many generic arguments in base class {next}", td);
                    return null;
                }

                TypeReference genericArg = genericType.GenericArguments[genericArgument];
                if (!genericArg.IsGenericParameter)
                {
                    // if not generic, sucessfully found type
                    return Weaver.CurrentAssembly.MainModule.ImportReference(genericArg);
                }
            }

            // nothing left in stack, something went wrong
            return null;
        }

        bool GetGenericBaseType(TypeDefinition td, TypeReference baseType, out GenericInstanceType found)
        {
            stack.Clear();
            TypeReference parent = td.BaseType;
            found = null;

            while (parent != null)
            {
                string parentName = parent.FullName;

                // strip generic parameters
                int index = parentName.IndexOf('<');
                if (index != -1)
                {
                    parentName = parentName.Substring(0, index);
                }

                if (parentName == baseType.FullName)
                {
                    found = parent as GenericInstanceType;
                    break;
                }
                try
                {
                    stack.Push(parent);
                    parent = parent.Resolve().BaseType;
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for plugins.
                    break;
                }
            }

            return found != null;
        }
    }
}
