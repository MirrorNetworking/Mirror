using System;
using System.Collections.Generic;
using Mono.CecilX;

namespace Mirror.Weaver
{
    public static class Extensions
    {
        public static bool IsDerivedFrom(this TypeDefinition td, TypeReference baseClass)
        {
            return IsDerivedFrom(td, baseClass.FullName);
        }

        public static bool IsDerivedFrom(this TypeDefinition td, string baseClassFullName)
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

                if (parentName == baseClassFullName)
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
            // jagged array
            if ((tr.IsArray && ((ArrayType)tr).ElementType.IsArray) ||
                // multidimensional array
                (tr.IsArray && ((ArrayType)tr).Rank > 1))
                return false;
            return true;
        }

        public static bool IsArraySegment(this TypeDefinition td)
        {
            return td.FullName.StartsWith("System.ArraySegment`1", System.StringComparison.Ordinal);
        }
        public static bool IsArraySegment(this TypeReference td)
        {
            return td.FullName.StartsWith("System.ArraySegment`1", System.StringComparison.Ordinal);
        }

        public static bool IsList(this TypeDefinition td)
        {
            return td.FullName.StartsWith("System.Collections.Generic.List`1", System.StringComparison.Ordinal);
        }
        public static bool IsList(this TypeReference td)
        {
            return td.FullName.StartsWith("System.Collections.Generic.List`1", System.StringComparison.Ordinal);
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


        /// <summary>
        /// Given a method of a generic class such as ArraySegment`T.get_Count,
        /// and a generic instance such as ArraySegment`int
        /// Creates a reference to the specialized method  ArraySegment`int`.get_Count 
        /// <para> Note that calling ArraySegment`T.get_Count directly gives an invalid IL error </para>
        /// </summary>
        /// <param name="self"></param>
        /// <param name="instanceType"></param>
        /// <returns></returns>
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

        public static CustomAttribute GetCustomAttribute(this ICustomAttributeProvider method, string attributeName)
        {
            foreach (CustomAttribute ca in method.CustomAttributes)
            {
                if (ca.AttributeType.FullName == attributeName)
                    return ca;
            }
            return null;
        }

        public static CustomAttribute GetCustomAttribute(this ICustomAttributeProvider method, TypeReference attribute)
        {
            foreach (CustomAttribute ca in method.CustomAttributes)
            {
                if (ca.AttributeType.FullName == attribute.FullName)
                    return ca;
            }
            return null;
        }

        public static bool HasCustomAttribute(this ICustomAttributeProvider attributeProvider, string attributeName)
        {
            foreach (CustomAttribute ca in attributeProvider.CustomAttributes)
            {
                if (ca.AttributeType.FullName == attributeName)
                    return true;
            }
            return false;
        }

        public static bool HasCustomAttribute(this ICustomAttributeProvider attributeProvider, TypeReference attribute)
        {
            foreach (CustomAttribute ca in attributeProvider.CustomAttributes)
            {
                if (ca.AttributeType.FullName == attribute.FullName)
                    return true;
            }
            return false;
        }

        public static T GetField<T>(this CustomAttribute ca, string field, T defaultValue)
        {
            foreach (CustomAttributeNamedArgument customField in ca.Fields)
            {
                if (customField.Name == field)
                {
                    return (T)customField.Argument.Value;
                }
            }

            return defaultValue;
        }

        public static MethodDefinition GetMethod(this TypeDefinition td, string methodName)
        {
            foreach (MethodDefinition md in td.Methods)
            {
                if (md.Name == methodName)
                    return md;
            }
            return null;
        }

        public static List<MethodDefinition> GetMethods(this TypeDefinition td, string methodName)
        {
            List<MethodDefinition> methods = new List<MethodDefinition>();
            foreach (MethodDefinition md in td.Methods)
            {
                if (md.Name == methodName)
                    methods.Add(md);
            }
            return methods;
        }

        public static MethodDefinition GetMethodInBaseType(this TypeDefinition td, string methodName)
        {
            TypeDefinition typedef = td;
            while (typedef != null)
            {
                foreach (MethodDefinition md in typedef.Methods)
                {
                    if (md.Name == methodName)
                        return md;
                }

                try
                {
                    TypeReference parent = typedef.BaseType;
                    typedef = parent?.Resolve();
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for pluins.
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="td"></param>
        /// <param name="methodName"></param>
        /// <param name="stopAt"></param>
        /// <returns></returns>
        public static bool HasMethodInBaseType(this TypeDefinition td, string methodName, TypeReference stopAt)
        {
            TypeDefinition typedef = td;
            while (typedef != null)
            {
                if (typedef.FullName == stopAt.FullName)
                    break;

                foreach (MethodDefinition md in typedef.Methods)
                {
                    if (md.Name == methodName)
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
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds public fields in type and base type
        /// </summary>
        /// <param name="variable"></param>
        /// <returns></returns>
        public static IEnumerable<FieldDefinition> FindAllPublicFields(this TypeReference variable)
        {
            return FindAllPublicFields(variable.Resolve());
        }

        /// <summary>
        /// Finds public fields in type and base type
        /// </summary>
        /// <param name="variable"></param>
        /// <returns></returns>
        public static IEnumerable<FieldDefinition> FindAllPublicFields(this TypeDefinition typeDefinition)
        {
            while (typeDefinition != null)
            {
                foreach (FieldDefinition field in typeDefinition.Fields)
                {
                    if (field.IsStatic || field.IsPrivate)
                        continue;

                    if (field.IsNotSerialized)
                        continue;

                    yield return field;
                }

                try
                {
                    typeDefinition = typeDefinition.BaseType.Resolve();
                }
                catch
                {
                    break;
                }
            }
        }
    }
}
