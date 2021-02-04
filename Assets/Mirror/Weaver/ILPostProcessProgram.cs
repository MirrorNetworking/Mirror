#if LEGACY_ILPP
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

using Assembly = System.Reflection.Assembly;

using ILPostProcessor = Mirror.Weaver.ILegacyPostProcessor;

namespace Mirror.Weaver
{
    internal static class ILPostProcessProgram
    {
        internal static ILPostProcessResult PostProcessResult;

        private static ILPostProcessor[] s_ILPostProcessors { get; set; }

        [InitializeOnLoadMethod]
        private static void OnInitializeOnLoad()
        {
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
            s_ILPostProcessors = FindAllPostProcessors();

            // this is for bootstrapping the weaver.
            // the very first time this is loaded, some assemblies have already been built.
            // so the hook will not trigger for them.
            // we need to reload all assemblies after subscribing to teh hook so they will
            // be weaved too

            Bootstrap();
        }

        private static void Bootstrap()
        {
            if (SessionState.GetBool("MIRROR_BOOTSTRAP", false))
                return;


            SessionState.SetBool("MIRROR_BOOTSTRAP", true);

            foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies())
            {
                if (File.Exists(assembly.outputPath))
                {
                    OnCompilationFinished(assembly.outputPath, new CompilerMessage[0]);
                }
            }

            Debug.Log("The weaver was bootstrapped.  Please reimport Mirror.Runtime once");

            EditorUtility.RequestScriptReload();
        }

        private static ILPostProcessor[] FindAllPostProcessors()
        {
            TypeCache.TypeCollection typesDerivedFrom = TypeCache.GetTypesDerivedFrom<ILPostProcessor>();
            var localILPostProcessors = new List<ILPostProcessor>(typesDerivedFrom.Count);

            foreach (Type typeCollection in typesDerivedFrom)
            {
                try
                {
                    localILPostProcessors.Add((ILPostProcessor)Activator.CreateInstance(typeCollection));
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Could not create ILPostProcessor ({typeCollection.FullName}):{Environment.NewLine}{exception.StackTrace}");
                }
            }

            // Default sort by type full name
            localILPostProcessors.Sort((left, right) => string.Compare(left.GetType().FullName, right.GetType().FullName, StringComparison.Ordinal));

            return localILPostProcessors.ToArray();
        }

        private static void OnCompilationFinished(string targetAssembly, CompilerMessage[] messages)
        {
            if (messages.Length > 0)
            {
                if (messages.Any(msg => msg.type == CompilerMessageType.Error))
                {
                    return;
                }
            }


            // Should not run on Unity Engine modules but we can run on the MLAPI Runtime DLL 
            if (targetAssembly.Contains("com.unity") || Path.GetFileName(targetAssembly).StartsWith("Unity"))
            {
                return;
            }

            Console.WriteLine($"Running Mirror ILPP on {targetAssembly}");

            string outputDirectory = $"{Application.dataPath}/../{Path.GetDirectoryName(targetAssembly)}";
            string unityEngine = string.Empty;
            string mlapiRuntimeAssemblyPath = string.Empty;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            bool usesMirror = false;
            bool foundThisAssembly = false;

            var depenencyPaths = new List<string>();
            foreach (Assembly assembly in assemblies)
            {
                // Find the assembly currently being compiled from domain assembly list and check if it's using unet
                if (assembly.GetName().Name == Path.GetFileNameWithoutExtension(targetAssembly))
                {
                    foundThisAssembly = true;
                    foreach (System.Reflection.AssemblyName dependency in assembly.GetReferencedAssemblies())
                    {
                        // Since this assembly is already loaded in the domain this is a no-op and returns the
                        // already loaded assembly
                        depenencyPaths.Add(Assembly.Load(dependency).Location);
                        if (dependency.Name.Contains(MirrorILPostProcessor.RuntimeAssemblyName))
                        {
                            usesMirror = true;
                        }
                    }
                }

                try
                {
                    if (assembly.Location.Contains("UnityEngine.CoreModule"))
                    {
                        unityEngine = assembly.Location;
                    }

                    if (assembly.Location.Contains(MirrorILPostProcessor.RuntimeAssemblyName))
                    {
                        mlapiRuntimeAssemblyPath = assembly.Location;
                    }
                }
                catch (NotSupportedException)
                {
                    // in memory assembly, can't get location
                }
            }

            if (!foundThisAssembly)
            {
                // Target assembly not found in current domain, trying to load it to check references 
                // will lead to trouble in the build pipeline, so lets assume it should go to weaver.
                // Add all assemblies in current domain to dependency list since there could be a 
                // dependency lurking there (there might be generated assemblies so ignore file not found exceptions).
                // (can happen in runtime test framework on editor platform and when doing full library reimport)
                foreach (Assembly assembly in assemblies)
                {
                    try
                    {
                        if (!(assembly.ManifestModule is System.Reflection.Emit.ModuleBuilder))
                        {
                            depenencyPaths.Add(Assembly.Load(assembly.GetName().Name).Location);
                        }
                    }
                    catch (FileNotFoundException)
                    {
                    }
                }

                usesMirror = true;
            }

            // We check if we are the MLAPI!
            if (!usesMirror)
            {
                // we shall also check and see if it we are ourself
                usesMirror = targetAssembly.Contains(MirrorILPostProcessor.RuntimeAssemblyName);
            }

            if (!usesMirror)
            {
                return;
            }

            if (string.IsNullOrEmpty(unityEngine))
            {
                Debug.LogError("Failed to find UnityEngine assembly");
                return;
            }

            if (string.IsNullOrEmpty(mlapiRuntimeAssemblyPath))
            {
                Debug.LogError("Failed to find mlapi runtime assembly");
                return;
            }

            string assemblyPathName = Path.GetFileName(targetAssembly);

            var targetCompiledAssembly = new ILPostProcessCompiledAssembly(assemblyPathName, depenencyPaths.ToArray(), null, outputDirectory);

            foreach (ILPostProcessor i in s_ILPostProcessors)
            {
                PostProcessResult = i.Process(targetCompiledAssembly);
                if (PostProcessResult == null)
                    continue;

                if (PostProcessResult.Diagnostics.Count > 0)
                {
                    Debug.LogError($"ILPostProcessor - {i.GetType().Name} failed to run on {targetCompiledAssembly.Name}");

                    foreach (DiagnosticMessage message in PostProcessResult.Diagnostics)
                    {
                        switch (message.DiagnosticType)
                        {
                            case DiagnosticType.Error:
                                Debug.LogError($"ILPostProcessor Error - {message.MessageData} {message.File} {message.Line} {message.Column}");
                                break;
                            case DiagnosticType.Warning:
                                Debug.LogWarning($"ILPostProcessor Warning - {message.MessageData} {message.File} {message.Line} {message.Column}");
                                break;
                        }
                    }

                    continue;
                }

                // we now need to write out the result?
                WriteAssembly(PostProcessResult.InMemoryAssembly, outputDirectory, assemblyPathName);
            }
        }

        static void WriteAssembly(InMemoryAssembly inMemoryAssembly, string outputPath, string assName)
        {
            Console.WriteLine($"Writing assembly {assName} to {outputPath}");

            if (inMemoryAssembly == null)
            {
                throw new ArgumentException("InMemoryAssembly has never been accessed or modified");
            }

            string asmPath = Path.Combine(outputPath, assName);
            string pdbFileName = $"{Path.GetFileNameWithoutExtension(assName)}.pdb";
            string pdbPath = Path.Combine(outputPath, pdbFileName);

            File.WriteAllBytes(asmPath, inMemoryAssembly.PeData);
            File.WriteAllBytes(pdbPath, inMemoryAssembly.PdbData);
        }
    }
}
#endif
