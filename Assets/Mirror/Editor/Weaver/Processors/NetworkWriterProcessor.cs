using System;
using Mono.CecilX;
using UnityEditor.Compilation;
using System.Linq;
using System.Collections.Generic;

namespace Mirror.Weaver
{
    public static class NetworkWriterProcessor
    {
       
        public static void LoadWriters(AssemblyDefinition currentAssembly, TypeDefinition klass)
        {
            // register all the writers in this class.  Skip the ones with wrong signature
            foreach (MethodDefinition method in klass.Methods)
            {
                if (method.Parameters.Count != 1)
                    continue;

                if (method.IsStatic)
                    continue;

                if (method.ReturnType.FullName != "System.Void")
                    continue;

                // disqusting
                // Not everything in NetworkWriter should be used,  we must cherry pick a few out
                // be very careful when adding any method to NetworkWriter,
                // it might be confused as a writer so you must add it here
                if (method.Name == "Write")
                    continue;
                if (method.Name == "WriteInt32")
                    continue;
                if (method.Name == "WriteUInt32")
                    continue;
                if (method.Name == "WriteInt64")
                    continue;
                if (method.Name == "WriteUInt64")
                    continue;
                if (method.Name == "set_Position")
                    continue;
                if (method.Name == "SetLength")
                    continue;


                TypeReference dataType = method.Parameters[0].ParameterType;
                Writers.Register(dataType, currentAssembly.MainModule.ImportReference(method));
            }
        }
    }
}
