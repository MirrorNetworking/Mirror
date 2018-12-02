// https://docs.unity3d.com/Manual/RunningEditorCodeOnLaunch.html
using System.IO;
using Mono.Cecil;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System;
using System.Linq;

namespace Mirror.Weaver
{
    // InitializeOnLoad is needed for Unity to call the static constructor on load
    [InitializeOnLoad]
    public class CompilationFinishedHook
    {
        static CompilationFinishedHook()
        {
            // assemblyPath: Library/ScriptAssemblies/Assembly-CSharp.dll/
            // assemblyPath: Library/ScriptAssemblies/Assembly-CSharp-Editor.dll
            CompilationPipeline.assemblyCompilationFinished += (assemblyPath, messages) =>
            {
                // if user scripts can't be compiled because of errors,
                // assemblyCompilationFinished is still called but assemblyPath
                // file won't exist. in that case, do nothing.
                if (!File.Exists(assemblyPath))
                {
                    Console.WriteLine("Weaving skipped because assembly doesnt exist: " + assemblyPath);
                    return;
                }

                // UnityEngineCoreModule.DLL path:
                string unityEngineCoreModuleDLL = UnityEditorInternal.InternalEditorUtility.GetEngineCoreModuleAssemblyPath();

                // outputDirectory is the directory of assemblyPath
                string outputDirectory = Path.GetDirectoryName(assemblyPath);

                string mirrorRuntimeDll = FindMirrorRuntime();
                if (!File.Exists(mirrorRuntimeDll))
                {
                    Debug.LogError("Could not find Mirror.Runtime.dll, make sure the file is in your project");
                    return;
                }

                if (assemblyPath == mirrorRuntimeDll)
                {
                    Debug.Log("Cannot weave mirror runtime");
                    return;
                }

                // unity calls it for Library/ScriptAssemblies/Assembly-CSharp-Editor.dll too, but we don't want to (and can't) weave this one
                bool buildingForEditor = assemblyPath.EndsWith("Editor.dll");
                if (!buildingForEditor)
                {
                    Console.WriteLine("Weaving: " + assemblyPath);
                    // assemblyResolver: unity uses this by default:
                    //   ICompilationExtension compilationExtension = GetCompilationExtension();
                    //   IAssemblyResolver assemblyResolver = compilationExtension.GetAssemblyResolver(editor, file, null);
                    // but Weaver creates it's own if null, which is this one:
                    IAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();
                    if (Program.Process(unityEngineCoreModuleDLL, mirrorRuntimeDll, outputDirectory, new string[] { assemblyPath }, GetExtraAssemblyPaths(assemblyPath), assemblyResolver, Debug.LogWarning, Debug.LogError))
                    {
                        Console.WriteLine("Weaving succeeded for: " + assemblyPath);
                    }
                    else
                    {
                        Debug.LogError("Weaving failed for: " + assemblyPath);
                    }
                }
            };
        }

        // Weaver needs the path for all the extra DLLs like UnityEngine.UI.
        // otherwise if a script that is being weaved (like a NetworkBehaviour)
        // uses UnityEngine.UI, then the Weaver won't be able to resolve it and
        // throw an error.
        // (the paths can be found by logging the extraAssemblyPaths in the
        //  original Weaver.Program.Process function.)
        static string[] GetExtraAssemblyPaths(string assemblyPath)
        {
            Assembly[] assemblies = CompilationPipeline.GetAssemblies();

            foreach (Assembly assembly in assemblies)
            {
                if (assembly.outputPath == assemblyPath)
                {
                    return assembly.compiledAssemblyReferences.Select(Path.GetDirectoryName).ToArray();
                }
            }

            Debug.LogWarning("Unable to find configuration for assembly " + assemblyPath);
            return new string[] { };
        }

        static string FindMirrorRuntime()
        {
            Assembly[] assemblies = CompilationPipeline.GetAssemblies();

            foreach (Assembly assembly in assemblies)
            {
                if (assembly.name == "Mirror")
                {
                    return assembly.outputPath;
                }
            }
            return "";
        }
    }
}