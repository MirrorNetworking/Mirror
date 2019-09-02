using System;
using Mono.CecilX;
using UnityEditor.Compilation;
using System.Linq;
using System.Collections.Generic;

namespace Mirror.Weaver
{
    public static class NetworkReaderProcessor
    {
       
        public static void LoadReaders(AssemblyDefinition currentAssembly, TypeDefinition klass)
        {
            // register all the reader in this class.  Skip the ones with wrong signature
            foreach (MethodDefinition method in klass.Methods)
            {
                if (method.Parameters.Count != 0)
                    continue;

                if (method.IsStatic)
                    continue;

                if (method.ReturnType.FullName == "System.Void")
                    continue;

                // disqusting
                if (method.Name == "ReadInt32")
                    continue;

                if (method.Name == "ReadUInt32")
                    continue;

                if (method.Name == "ReadInt64")
                    continue;

                if (method.Name == "ReadUInt64")
                    continue;

                if (method.Name == "ToString")
                    continue;

                if (method.Name == "get_Length")
                    continue;


                Readers.Register(method.ReturnType, currentAssembly.MainModule.ImportReference(method));
            }
        }
    }
}
