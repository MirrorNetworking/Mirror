using System;
using Mono.CecilX;

namespace Mirror.Weaver
{
    public static class Extensions
    {
        public static bool IsDerivedFrom(this TypeDefinition td, TypeReference baseClass)
        {
            if (!td.IsClass)
                return false;

            // are ANY parent classes of baseClass?
            TypeReference parent = td.BaseType;
            while (parent != null)
            {
                string parentName = parent.FullName;

                // strip generic parameters
                int index = parentName.IndexOf('<');
                if (index != -1)
                {
                    parentName = parentName.Substring(0, index);
                }

                if (parentName == baseClass.FullName)
                {
                    return true;
                }
                try
                {
                    parent = parent.Resolve().BaseType;
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for plugins.
                    //Console.WriteLine("AssemblyResolutionException: "+ ex.ToString());
                    break;
                }
            }
            return false;
        }

        public static TypeReference GetEnumUnderlyingType(this TypeDefinition td)
        {
            foreach (FieldDefinition field in td.Fields)
            {
                if (!field.IsStatic)
                    return field.FieldType;
            }
            throw new ArgumentException($"Invalid enum {td.FullName}");
        }

        public static bool ImplementsInterface(this TypeDefinition td, TypeReference baseInterface)
        {
            TypeDefinition typedef = td;
            while (typedef != null)
            {
                foreach (InterfaceImplementation iface in typedef.Interfaces)
                {
                    if (iface.InterfaceType.FullName == baseInterface.FullName)
                        return true;
                }

                try
                {
                    TypeReference parent = typedef.BaseType;
                    typedef = parent?.Resolve();
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for pluins.
                    //Console.WriteLine("AssemblyResolutionException: "+ ex.ToString());
                    break;
                }
            }

            return false;
        }

        public static bool IsArrayType(this TypeReference tr)
        {
            if ((tr.IsArray && ((ArrayType)tr).ElementType.IsArray) || // jagged array
                (tr.IsArray && ((ArrayType)tr).Rank > 1)) // multidimensional array
                return false;
            return true;
        }

        public static bool CanBeResolved(this TypeReference parent)
        {
            while (parent != null)
            {
                if (parent.Scope.Name == "Windows")
                {
                    return false;
                }

                if (parent.Scope.Name == "mscorlib")
                {
                    TypeDefinition resolved = parent.Resolve();
                    return resolved != null;
                }

                try
                {
                    parent = parent.Resolve().BaseType;
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }


        // Given a method of a generic class such as ArraySegment<T>.get_Count,   
        // and a generic instance such as ArraySegment<int>
        // Creates a reference to the specialized method  ArraySegment<int>.get_Count;
        // Note that calling ArraySegment<T>.get_Count directly gives an invalid IL error
        public static MethodReference MakeHostInstanceGeneric(this MethodReference self, GenericInstanceType instanceType)
        {

            MethodReference reference = new MethodReference(self.Name, self.ReturnType, instanceType)
            {
                CallingConvention = self.CallingConvention,
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis
            };

            foreach (ParameterDefinition parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (GenericParameter generic_parameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

            return Weaver.CurrentAssembly.MainModule.ImportReference(reference);
        }

        public static CustomAttribute GetCustomAttribute(this MethodDefinition method, string attributeName)
        {
            foreach (CustomAttribute ca in method.CustomAttributes)
            {
                if (ca.AttributeType.FullName == attributeName)
                    return ca;
            }
            return null;
        }

    }
}
