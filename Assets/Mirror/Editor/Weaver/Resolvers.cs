// all the resolve functions for the weaver
// NOTE: these functions should be made extensions, but right now they still
//       make heavy use of Weaver.fail and we'd have to check each one's return
//       value for null otherwise.
//       (original FieldType.Resolve returns null if not found too, so
//        exceptions would be a bit inconsistent here)
using Mono.CecilX;

namespace Mirror.Weaver
{
    public static class Resolvers
    {
        public static MethodReference ResolveMethod(TypeReference tr, AssemblyDefinition scriptDef, string name)
        {
            if (tr == null)
            {
                Weaver.Error($"Cannot resolve method {name} without a class");
                return null;
            }
            MethodReference method = ResolveMethod(tr, scriptDef, m => m.Name == name);
            if (method == null)
            {
                Weaver.Error($"Method not found with name {name} in type {tr.Name}", tr);
            }
            return method;
        }

        public static MethodReference ResolveMethod(TypeReference t, AssemblyDefinition scriptDef, System.Func<MethodDefinition, bool> predicate)
        {
            foreach (MethodDefinition methodRef in t.Resolve().Methods)
            {
                if (predicate(methodRef))
                {
                    return scriptDef.MainModule.ImportReference(methodRef);
                }
            }

            Weaver.Error($"Method not found in type {t.Name}", t);
            return null;
        }

        public static MethodReference ResolveMethodInParents(TypeReference tr, AssemblyDefinition scriptDef, string name)
        {
            if (tr == null)
            {
                Weaver.Error($"Cannot resolve method {name} without a type");
                return null;
            }
            foreach (MethodDefinition methodRef in tr.Resolve().Methods)
            {
                if (methodRef.Name == name)
                {
                    return scriptDef.MainModule.ImportReference(methodRef);
                }
            }
            // Could not find the method in this class,  try the parent
            return ResolveMethodInParents(tr.Resolve().BaseType, scriptDef, name);
        }

        // System.Byte[] arguments need a version with a string
        public static MethodReference ResolveMethodWithArg(TypeReference tr, AssemblyDefinition scriptDef, string name, string argTypeFullName)
        {
            bool match(MethodDefinition method) =>
                    method.Name == name
                    && (method.Parameters.Count == 1)
                    && method.Parameters[0].ParameterType.FullName == argTypeFullName;

            return ResolveMethod(tr, scriptDef, match);
        }

        // reuse ResolveMethodWithArg string version
        public static MethodReference ResolveMethodWithArg(TypeReference tr, AssemblyDefinition scriptDef, string name, TypeReference argType)
        {
            return ResolveMethodWithArg(tr, scriptDef, name, argType.FullName);
        }

        public static MethodDefinition ResolveDefaultPublicCtor(TypeReference variable)
        {
            foreach (MethodDefinition methodRef in variable.Resolve().Methods)
            {
                if (methodRef.Name == ".ctor" &&
                    methodRef.Resolve().IsPublic &&
                    methodRef.Parameters.Count == 0)
                {
                    return methodRef;
                }
            }
            return null;
        }

        public static GenericInstanceMethod ResolveMethodGeneric(TypeReference t, AssemblyDefinition scriptDef, string name, TypeReference genericType)
        {
            foreach (MethodDefinition methodRef in t.Resolve().Methods)
            {
                if (methodRef.Name == name && methodRef.Parameters.Count == 0 && methodRef.GenericParameters.Count == 1)
                {
                    MethodReference tmp = scriptDef.MainModule.ImportReference(methodRef);
                    GenericInstanceMethod gm = new GenericInstanceMethod(tmp);
                    gm.GenericArguments.Add(genericType);
                    if (gm.GenericArguments[0].FullName == genericType.FullName)
                    {
                        return gm;
                    }
                }
            }

            Weaver.Error($"Method {name} not found in {t.Name}", t);
            return null;
        }

        public static MethodReference ResolveProperty(TypeReference tr, AssemblyDefinition scriptDef, string name)
        {
            foreach (PropertyDefinition pd in tr.Resolve().Properties)
            {
                if (pd.Name == name)
                {
                    return scriptDef.MainModule.ImportReference(pd.GetMethod);
                }
            }
            return null;
        }
    }
}
