using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;

namespace Mirror.Weaver
{

    public class Weaver
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

                var types = new List<TypeDefinition>(module.Types);

                foreach (TypeDefinition td in types)
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

        public static AssemblyDefinition AssemblyDefinitionFor(ICompiledAssembly compiledAssembly)
        {
            var assemblyResolver = new PostProcessorAssemblyResolver(compiledAssembly);
            var readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = assemblyResolver,
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                ReadingMode = ReadingMode.Immediate
            };

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(new MemoryStream(compiledAssembly.InMemoryAssembly.PeData), readerParameters);

            //apparently, it will happen that when we ask to resolve a type that lives inside MLAPI.Runtime, and we
            //are also postprocessing MLAPI.Runtime, type resolving will fail, because we do not actually try to resolve
            //inside the assembly we are processing. Let's make sure we do that, so that we can use postprocessor features inside
            //MLAPI.Runtime itself as well.
            assemblyResolver.AddAssemblyDefinitionBeingOperatedOn(assemblyDefinition);

            return assemblyDefinition;
        }

        public AssemblyDefinition Weave(ICompiledAssembly compiledAssembly)
        {
            try
            {
                CurrentAssembly = AssemblyDefinitionFor(compiledAssembly);

                ModuleDefinition module = CurrentAssembly.MainModule;
                readers = new Readers(module, logger);
                writers = new Writers(module, logger);
                var rwstopwatch = System.Diagnostics.Stopwatch.StartNew();
                propertySiteProcessor = new PropertySiteProcessor();
                var rwProcessor = new ReaderWriterProcessor(module, readers, writers);

                bool modified = rwProcessor.Process();
                rwstopwatch.Stop();
                Console.WriteLine($"Find all reader and writers took {rwstopwatch.ElapsedMilliseconds} milliseconds");

                Console.WriteLine($"Script Module: {module.Name}");

                modified |= WeaveModule(module);

                if (!modified)
                    return CurrentAssembly;

                rwProcessor.InitializeReaderAndWriters();

                return CurrentAssembly;
            }
            catch (Exception e)
            {
                logger.Error("Exception :" + e);
                return null;
            }
        }
    }
}
