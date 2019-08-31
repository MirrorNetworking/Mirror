using System;
using Mono.CecilX;
using UnityEditor.Compilation;
using System.Linq;
using System.Collections.Generic;

namespace Mirror.Weaver
{

    public static class ReaderWriterProcessor
    {

        // find all readers and writers and register them
        public static void ProcessReadersAndWriters(AssemblyDefinition CurrentAssembly)
        {
            Readers.Init();
            Writers.Init();

            foreach (Assembly unityAsm in CompilationPipeline.GetAssemblies())
            {
                if (unityAsm.name != CurrentAssembly.Name.Name)
                {
                    try
                    {
                        using (DefaultAssemblyResolver asmResolver = new DefaultAssemblyResolver())
                        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(unityAsm.outputPath, new ReaderParameters { ReadWrite = false, ReadSymbols = true, AssemblyResolver = asmResolver }))
                        {
                            ProcessAssemblyClasses(CurrentAssembly, assembly);
                        }
                    }
                    catch
                    {
                        // some assemblies cannot be loaded,  it is ok
                    }
                }
            }

            ProcessAssemblyClasses(CurrentAssembly, CurrentAssembly);
        }

        private static void ProcessAssemblyClasses(AssemblyDefinition CurrentAssembly, AssemblyDefinition assembly)
        {
            // find all the classes with writers
            IEnumerable<TypeDefinition> writerClasses = from klass in assembly.MainModule.Types
                                where klass.CustomAttributes.Any(attr => attr.AttributeType.FullName == "Mirror.NetworkWriterAttribute")
                                select klass;

            foreach (var klass in writerClasses)
            {
                LoadWriters(CurrentAssembly, klass);
            }

            IEnumerable<TypeDefinition> readerClasses = from klass in assembly.MainModule.Types
                                where klass.CustomAttributes.Any(attr => attr.AttributeType.FullName == "Mirror.NetworkReaderAttribute")
                                select klass;

            foreach (var klass in readerClasses)
            {
                LoadReaders(CurrentAssembly, klass);
            }

        }

        private static void LoadWriters(AssemblyDefinition currentAssembly, TypeDefinition klass)
        {
            // register all the writers in this class
            foreach (MethodDefinition method in klass.Methods)
            {
                // method must have 2 parameters
                if (method.Parameters.Count != 2)
                    continue;

                // first parameter must be a NetworkWriter
                if (method.Parameters[0].ParameterType.FullName != "Mirror.NetworkWriter")
                    continue;

                // method must be void
                if (method.ReturnType.FullName != "System.Void")
                    continue;

                TypeReference dataType = method.Parameters[1].ParameterType;

                Writers.Register(dataType, currentAssembly.MainModule.ImportReference(method));
            }
        }

        private static void LoadReaders(AssemblyDefinition currentAssembly, TypeDefinition klass)
        {
            // register all the writers in this class
            foreach (MethodDefinition method in klass.Methods)
            {
                // method must have 2 parameters
                if (method.Parameters.Count != 1)
                    continue;

                // first parameter must be a NetworkWriter
                if (method.Parameters[0].ParameterType.FullName != "Mirror.NetworkReader")
                    continue;

                // method must be void
                if (method.ReturnType.FullName == "System.Void")
                    continue;

                TypeReference dataType = method.ReturnType;

                Readers.Register(dataType, currentAssembly.MainModule.ImportReference(method));
            }
        }
    }

}