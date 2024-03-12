// based on paul's resolver from
// https://github.com/MirageNet/Mirage/commit/def64cd1db525398738f057b3d1eb1fe8afc540c?branch=def64cd1db525398738f057b3d1eb1fe8afc540c&diff=split
//
// an assembly resolver's job is to open an assembly in case we want to resolve
// a type from it.
//
// for example, while weaving MyGame.dll: if we want to resolve ArraySegment<T>,
// then we need to open and resolve from another assembly (CoreLib).
//
// using DefaultAssemblyResolver with ILPostProcessor throws Exceptions in
// WeaverTypes.cs when resolving anything, for example:
// ArraySegment<T> in Mirror.Tests.Dll.
//
// we need a custom resolver for ILPostProcessor.
#if UNITY_2020_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using Mono.CecilX;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Mirror.Weaver
{
    class ILPostProcessorAssemblyResolver : IAssemblyResolver
    {
        readonly string[] assemblyReferences;

        // originally we used Dictionary + lock.
        // Resolve() is called thousands of times for large projects.
        // ILPostProcessor is multithreaded, so best to use ConcurrentDictionary without the lock here.
        readonly ConcurrentDictionary<string, AssemblyDefinition> assemblyCache =
            new ConcurrentDictionary<string, AssemblyDefinition>();

        // Resolve() calls FindFile() every time.
        // thousands of times for String => mscorlib alone in large projects.
        // cache the results! ILPostProcessor is multithreaded, so use a ConcurrentDictionary here.
        readonly ConcurrentDictionary<string, string> fileNameCache =
            new ConcurrentDictionary<string, string>();

        readonly ICompiledAssembly compiledAssembly;
        AssemblyDefinition selfAssembly;

        readonly Logger Log;

        public ILPostProcessorAssemblyResolver(ICompiledAssembly compiledAssembly, Logger Log)
        {
            this.compiledAssembly = compiledAssembly;
            assemblyReferences = compiledAssembly.References;
            this.Log = Log;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Cleanup
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name) =>
            Resolve(name, new ReaderParameters(ReadingMode.Deferred));

        // here is an example on when this is called:
        //   Player : NetworkBehaviour has a [SyncVar] of type String.
        //     Weaver's SyncObjectInitializer checks if ImplementsSyncObject()
        //       which needs to resolve the type 'String' from mscorlib.
        //         Resolve() lives in CecilX.MetadataResolver.Resolve()
        //           which calls assembly_resolver.Resolve().
        //             which uses our ILPostProcessorAssemblyResolver here.
        //
        // for large projects, this is called thousands of times for mscorlib alone.
        // initially ILPostProcessorAssemblyResolver took 30x longer than with CompilationFinishedHook.
        // we need to cache and speed up everything we can here!
        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (name.Name == compiledAssembly.Name)
                return selfAssembly;

            // cache FindFile.
            // in large projects, this is called thousands(!) of times for String=>mscorlib alone.
            // reduces a single String=>mscorlib resolve from 0.771ms to 0.015ms.
            // => 50x improvement in TypeReference.Resolve() speed!
            // => 22x improvement in Weaver speed!
            if (!fileNameCache.TryGetValue(name.Name, out string fileName))
            {
                fileName = FindFile(name.Name);
                fileNameCache.TryAdd(name.Name, fileName);
            }

            if (fileName == null)
            {
                // returning null will throw exceptions in our weaver where.
                // let's make it obvious why we returned null for easier debugging.
                // NOTE: if this fails for "System.Private.CoreLib":
                //       ILPostProcessorReflectionImporter fixes it!

                // the fix for #2503 started showing this warning for Bee.BeeDriver on mac,
                // which is for compilation. we can ignore that one.
                if (!name.Name.StartsWith("Bee.BeeDriver"))
                {
                    Log.Warning($"ILPostProcessorAssemblyResolver.Resolve: Failed to find file for {name}");
                }
                return null;
            }

            // try to get cached assembly by filename + writetime
            DateTime lastWriteTime = File.GetLastWriteTime(fileName);
            string cacheKey = fileName + lastWriteTime;
            if (assemblyCache.TryGetValue(cacheKey, out AssemblyDefinition result))
                return result;

            // otherwise resolve and cache a new assembly
            parameters.AssemblyResolver = this;
            MemoryStream ms = MemoryStreamFor(fileName);

            string pdb = fileName + ".pdb";
            if (File.Exists(pdb))
                parameters.SymbolStream = MemoryStreamFor(pdb);

            AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(ms, parameters);
            assemblyCache.TryAdd(cacheKey, assemblyDefinition);
            return assemblyDefinition;
        }

        // find assemblyname in assembly's references
        string FindFile(string name)
        {
            // perhaps the type comes from a .dll or .exe
            // check both in one call without Linq instead of iterating twice like originally
            foreach (string r in assemblyReferences)
            {
                if (Path.GetFileNameWithoutExtension(r) == name)
                    return r;
            }

            // this is called thousands(!) of times.
            // constructing strings only once saves ~0.1ms per call for mscorlib.
            string dllName = name + ".dll";

            // Unfortunately the current ICompiledAssembly API only provides direct references.
            // It is very much possible that a postprocessor ends up investigating a type in a directly
            // referenced assembly, that contains a field that is not in a directly referenced assembly.
            // if we don't do anything special for that situation, it will fail to resolve.  We should fix this
            // in the ILPostProcessing API. As a workaround, we rely on the fact here that the indirect references
            // are always located next to direct references, so we search in all directories of direct references we
            // got passed, and if we find the file in there, we resolve to it.
            foreach (string parentDir in assemblyReferences.Select(Path.GetDirectoryName).Distinct())
            {
                string candidate = Path.Combine(parentDir, dllName);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        // open file as MemoryStream.
        // ILPostProcessor is multithreaded.
        // retry a few times in case another thread is still accessing the file.
        static MemoryStream MemoryStreamFor(string fileName)
        {
            return Retry(10, TimeSpan.FromSeconds(1), () =>
            {
                byte[] byteArray;
                using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byteArray = new byte[fs.Length];
                    int readLength = fs.Read(byteArray, 0, (int)fs.Length);
                    if (readLength != fs.Length)
                        throw new InvalidOperationException("File read length is not full length of file.");
                }

                return new MemoryStream(byteArray);
            });
        }

        static MemoryStream Retry(int retryCount, TimeSpan waitTime, Func<MemoryStream> func)
        {
            try
            {
                return func();
            }
            catch (IOException)
            {
                if (retryCount == 0)
                    throw;
                Console.WriteLine($"Caught IO Exception, trying {retryCount} more times");
                Thread.Sleep(waitTime);
                return Retry(retryCount - 1, waitTime, func);
            }
        }

        // if the CompiledAssembly's AssemblyDefinition is known, we can add it
        public void SetAssemblyDefinitionForCompiledAssembly(AssemblyDefinition assemblyDefinition)
        {
            selfAssembly = assemblyDefinition;
        }
    }
}
#endif
