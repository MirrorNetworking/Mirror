using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Mono.CecilX;
using Mono.CecilX.Cil;
using Mono.CecilX.Mdb;
using Mono.CecilX.Pdb;

namespace Mirror.Weaver
{
    class Helpers
    {
        // This code is taken from SerializationWeaver

        class AddSearchDirectoryHelper
        {
            delegate void AddSearchDirectoryDelegate(string directory);
            readonly AddSearchDirectoryDelegate _addSearchDirectory;

            public AddSearchDirectoryHelper(IAssemblyResolver assemblyResolver)
            {
                // reflection is used because IAssemblyResolver doesn't implement AddSearchDirectory but both DefaultAssemblyResolver and NuGetAssemblyResolver do
                MethodInfo addSearchDirectory = assemblyResolver.GetType().GetMethod("AddSearchDirectory", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(string) }, null);
                if (addSearchDirectory == null)
                    throw new Exception("Assembly resolver doesn't implement AddSearchDirectory method.");
                _addSearchDirectory = (AddSearchDirectoryDelegate)Delegate.CreateDelegate(typeof(AddSearchDirectoryDelegate), assemblyResolver, addSearchDirectory);
            }

            public void AddSearchDirectory(string directory)
            {
                _addSearchDirectory(directory);
            }
        }

        public static string UnityEngineDLLDirectoryName()
        {
            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            return directoryName?.Replace(@"file:\", "");
        }

        public static ISymbolReaderProvider GetSymbolReaderProvider(string inputFile)
        {
            string nakedFileName = inputFile.Substring(0, inputFile.Length - 4);
            if (File.Exists(nakedFileName + ".pdb"))
            {
                Console.WriteLine("Symbols will be read from " + nakedFileName + ".pdb");
                return new PdbReaderProvider();
            }
            if (File.Exists(nakedFileName + ".dll.mdb"))
            {
                Console.WriteLine("Symbols will be read from " + nakedFileName + ".dll.mdb");
                return new MdbReaderProvider();
            }
            Console.WriteLine("No symbols for " + inputFile);
            return null;
        }

        public static string DestinationFileFor(string outputDir, string assemblyPath)
        {
            string fileName = Path.GetFileName(assemblyPath);
            Debug.Assert(fileName != null, "fileName != null");

            return Path.Combine(outputDir, fileName);
        }

        public static string PrettyPrintType(TypeReference type)
        {
            // generic instances, such as List<Int32>
            if (type.IsGenericInstance)
            {
                GenericInstanceType giType = (GenericInstanceType)type;
                return giType.Name.Substring(0, giType.Name.Length - 2) + "<" + string.Join(", ", giType.GenericArguments.Select(PrettyPrintType).ToArray()) + ">";
            }

            // generic types, such as List<T>
            if (type.HasGenericParameters)
            {
                return type.Name.Substring(0, type.Name.Length - 2) + "<" + string.Join(", ", type.GenericParameters.Select(x => x.Name).ToArray()) + ">";
            }

            // non-generic type such as Int
            return type.Name;
        }

        public static ReaderParameters ReaderParameters(string assemblyPath, IEnumerable<string> extraPaths, IAssemblyResolver assemblyResolver, string unityEngineDLLPath, string mirrorNetDLLPath)
        {
            ReaderParameters parameters = new ReaderParameters {ReadWrite = true};
            if (assemblyResolver == null)
                assemblyResolver = new DefaultAssemblyResolver();
            AddSearchDirectoryHelper helper = new AddSearchDirectoryHelper(assemblyResolver);
            helper.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));
            helper.AddSearchDirectory(UnityEngineDLLDirectoryName());
            helper.AddSearchDirectory(Path.GetDirectoryName(unityEngineDLLPath));
            helper.AddSearchDirectory(Path.GetDirectoryName(mirrorNetDLLPath));
            if (extraPaths != null)
            {
                foreach (string path in extraPaths)
                    helper.AddSearchDirectory(path);
            }
            parameters.AssemblyResolver = assemblyResolver;
            parameters.SymbolReaderProvider = GetSymbolReaderProvider(assemblyPath);
            return parameters;
        }

        public static WriterParameters GetWriterParameters(ReaderParameters readParams)
        {
            WriterParameters writeParams = new WriterParameters();
            if (readParams.SymbolReaderProvider is PdbReaderProvider)
            {
                //Log("Will export symbols of pdb format");
                writeParams.SymbolWriterProvider = new PdbWriterProvider();
            }
            else if (readParams.SymbolReaderProvider is MdbReaderProvider)
            {
                //Log("Will export symbols of mdb format");
                writeParams.SymbolWriterProvider = new MdbWriterProvider();
            }
            return writeParams;
        }
    }
}
