// all the resolve functions for the weaver
// NOTE: these functions should be made extensions, but right now they still
//       make heavy use of Weaver.fail and we'd have to check each one's return
//       value for null otherwise.
//       (original FieldType.Resolve returns null if not found too, so
//        exceptions would be a bit inconsistent here)
using Mono.Cecil;

namespace Mirror.Weaver
{
    public static class Resolvers
    {
        public static MethodReference ResolveMethod(TypeReference tr, AssemblyDefinition scriptDef, string name)
        {
            //Console.WriteLine("ResolveMethod " + t.ToString () + " " + name);
            if (tr == null)
            {
                Log.Error("Type missing for " + name);
                Weaver.fail = true;
                return null;
            }
            foreach (MethodDefinition methodRef in tr.Resolve().Methods)
            {
                if (methodRef.Name == name)
                {
                    return scriptDef.MainModule.ImportReference(methodRef);
                }
            }
            Log.Error("ResolveMethod failed " + tr.Name + "::" + name + " " + tr.Resolve());

            // why did it fail!?
            foreach (MethodDefinition methodRef in tr.Resolve().Methods)
            {
                Log.Error("Method " + methodRef.Name);
            }

            Weaver.fail = true;
            return null;
        }
    }
}