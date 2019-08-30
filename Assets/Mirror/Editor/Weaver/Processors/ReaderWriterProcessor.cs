using System;
using Mono.CecilX;
using UnityEditor.Compilation;

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
                            ProcessAssemblyAttributes(CurrentAssembly, assembly);
                        }
                    }
                    catch
                    {
                        // some assemblies cannot be loaded,  it is ok
                    }
                }
            }

            ProcessAssemblyAttributes(CurrentAssembly, CurrentAssembly);
        }

        private static void ProcessAssemblyAttributes(AssemblyDefinition CurrentAssembly, AssemblyDefinition assembly)
        {
            foreach (CustomAttribute attribute in assembly.CustomAttributes)
            {
                if (attribute.AttributeType.FullName == "Mirror.ReaderWriterAttribute")
                {
                    LoadReaderWriter(CurrentAssembly, attribute);
                }
            }
        }

        private static void LoadReaderWriter(AssemblyDefinition CurrentAssembly, CustomAttribute attribute)
        {
            TypeReference typeClass = (TypeReference)attribute.ConstructorArguments[0].Value;
            TypeReference readerClass = (TypeReference)attribute.ConstructorArguments[1].Value;
            string readerMethod = (string)attribute.ConstructorArguments[2].Value;
            TypeReference writerClass = (TypeReference)attribute.ConstructorArguments[3].Value;
            string writerMethod = (string)attribute.ConstructorArguments[4].Value;

            Readers.Register(typeClass, Resolvers.ResolveMethod(readerClass, CurrentAssembly, readerMethod));
            Writers.Register(typeClass, Resolvers.ResolveMethod(writerClass, CurrentAssembly, writerMethod));

        }

    }

}