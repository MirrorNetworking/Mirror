using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using UnityEditor.Compilation;

namespace Mirror.Weaver
{

    internal class Weaver
    {
        private readonly IWeaverLogger logger;
        private Readers readers;
        private Writers writers;
        private PropertySiteProcessor propertySiteProcessor;

        private AssemblyDefinition CurrentAssembly { get; set; }

        public static void DLog(TypeDefinition td, string fmt, params object[] args)
        {
            Console.WriteLine("[" + td.Name + "] " + string.Format(fmt, args));
        }

        public Weaver(IWeaverLogger logger)
        {
            this.logger = logger;
        }

        void CheckMonoBehaviour(TypeDefinition td)
        {
            var processor = new MonoBehaviourProcessor(logger);

            if (td.IsDerivedFrom<UnityEngine.MonoBehaviour>())
            {
                processor.Process(td);
            }
        }

        bool WeaveNetworkBehavior(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            if (!td.IsDerivedFrom<NetworkBehaviour>())
            {
                CheckMonoBehaviour(td);
                return false;
            }

            // process this and base classes from parent to child order

            var behaviourClasses = new List<TypeDefinition>();

            TypeDefinition parent = td;
            while (parent != null)
            {
                if (parent.Is<NetworkBehaviour>())
                {
                    break;
                }

                try
                {
                    behaviourClasses.Insert(0, parent);
                    parent = parent.BaseType.Resolve();
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for plugins.
                    break;
                }
            }

            bool modified = false;
            foreach (TypeDefinition behaviour in behaviourClasses)
            {
                modified |= new NetworkBehaviourProcessor(behaviour, readers, writers, propertySiteProcessor, logger).Process();
            }
            return modified;
        }

        bool WeaveModule(ModuleDefinition module)
        {
            try
            {
                bool modified = false;

                var watch = System.Diagnostics.Stopwatch.StartNew();

                watch.Start();
                var attributeProcessor = new ServerClientAttributeProcessor(logger);

                foreach (TypeDefinition td in module.Types)
                {
                    if (td.IsClass && td.BaseType.CanBeResolved())
                    {
                        modified |= WeaveNetworkBehavior(td);
                        modified |= attributeProcessor.Process(td);
                    }
                }
                watch.Stop();
                Console.WriteLine("Weave behaviours and messages took" + watch.ElapsedMilliseconds + " milliseconds");

                if (modified)
                    propertySiteProcessor.Process(module);

                return modified;
            }
            catch (Exception ex)
            {
                logger.Error(ex.ToString());
                throw;
            }
        }

        bool Weave(Assembly unityAssembly)
        {
            using (var asmResolver = new DefaultAssemblyResolver())
            using (CurrentAssembly = AssemblyDefinition.ReadAssembly(unityAssembly.outputPath, new ReaderParameters { ReadWrite = true, ReadSymbols = true, AssemblyResolver = asmResolver }))
            {

                AddPaths(asmResolver, unityAssembly);
                ModuleDefinition module = CurrentAssembly.MainModule;
                readers = new Readers(module, logger);
                writers = new Writers(module, logger);
                propertySiteProcessor = new PropertySiteProcessor();
                var rwProcessor = new ReaderWriterProcessor(module, readers, writers);

                var rwstopwatch = System.Diagnostics.Stopwatch.StartNew();
                bool modified = rwProcessor.Process(unityAssembly);
                rwstopwatch.Stop();
                Console.WriteLine($"Find all reader and writers took {rwstopwatch.ElapsedMilliseconds} milliseconds");

                Console.WriteLine($"Script Module: {module.Name}");

                modified |= WeaveModule(module);

                if (modified)
                {
                    rwProcessor.InitializeReaderAndWriters();

                    // write to outputDir if specified, otherwise perform in-place write
                    var writeParams = new WriterParameters { WriteSymbols = true };
                    CurrentAssembly.Write(writeParams);
                }
            }

            return true;
        }

        private static void AddPaths(DefaultAssemblyResolver asmResolver, Assembly assembly)
        {
            foreach (string path in assembly.allReferences)
            {
                asmResolver.AddSearchDirectory(Path.GetDirectoryName(path));
            }
        }

        public bool WeaveAssembly(Assembly assembly)
        {
            try
            {
                return Weave(assembly);
            }
            catch (Exception e)
            {
                logger.Error("Exception :" + e);
                return false;
            }
        }
    }
}
