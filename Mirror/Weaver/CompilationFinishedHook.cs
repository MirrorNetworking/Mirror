// https://docs.unity3d.com/Manual/RunningEditorCodeOnLaunch.html
using System.IO;
using Mono.Cecil;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System;

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
                // UnityEngineCoreModule.DLL path:
                string unityEngineCoreModuleDLL = UnityEditorInternal.InternalEditorUtility.GetEngineCoreModuleAssemblyPath();

                // outputDirectory is the directory of assemblyPath
                string outputDirectory = Path.GetDirectoryName(assemblyPath);

                string mirrorRuntimeDll = FindMirrorRuntime();

                if (!File.Exists(mirrorRuntimeDll))
                {
                    Debug.LogError("Could not find Mirror runtime,  make sure the file " + mirrorRuntimeDll + " is in your project");
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
                    if (Program.Process(unityEngineCoreModuleDLL, mirrorRuntimeDll, outputDirectory, new string[1] { assemblyPath }, new string[0], assemblyResolver, Debug.LogWarning, Debug.LogError))
                    {
                        Console.WriteLine("Weaving succeeded for: " + assemblyPath);
                    }
                    else
                        Debug.LogError("Weaving failed for: " + assemblyPath);
                }
            };

        }

        private static string FindMirrorRuntime()
        {
            string path = Path.Combine("Assets", "Plugins");
            path = Path.Combine(path, "Mirror.Runtime.dll");

            return path;
        }

    }
}