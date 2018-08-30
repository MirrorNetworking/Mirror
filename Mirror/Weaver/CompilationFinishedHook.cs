// https://docs.unity3d.com/Manual/RunningEditorCodeOnLaunch.html
using System.IO;
using Mono.Cecil;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System;
using System.Linq;
using UnityEditor.Events;
using System.Reflection;

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
                    if (Program.Process(unityEngineCoreModuleDLL, mirrorRuntimeDll, outputDirectory, new string[1] { assemblyPath }, GetExtraAssemblyPaths(assemblyPath), assemblyResolver, Debug.LogWarning, Debug.LogError))
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
            var assemblies = CompilationPipeline.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                if (assembly.outputPath == assemblyPath)
                {
                    return assembly.compiledAssemblyReferences.Select(Path.GetDirectoryName).ToArray();
                }
            }
            return new string[] { };
        }

        static string FindMirrorRuntime()
        {
            // we can't assume that Mirror.Runtime.dll is always at the same
            // path, because some people might move the 'Mirror' folder into
            // another folder, etc.
            // -> can't check loaded assemblies/assets because this happens
            //    after compiling, before load
            // -> search assets folder instead and cache result
            // -> we have Runtime and Runtime-Editor dll. it doesn't matter
            //    which one we use, so let's always use the one that is found
            //    first

            // searching huge project directories can be expensive, so let's use
            // EditorPrefs to try the last working one first
            // -> EditorPrefs are global across projects. we only care about the
            //    path for this project though. otherwise switching between two
            //    projects would need path to be searched again each time
            // -> use project GUID to make project specific paths
            string key = PlayerSettings.productGUID + ".LastMirrorRuntimeDll";
            if (EditorPrefs.HasKey(key))
            {
                string lastPath = EditorPrefs.GetString(key);
                if (File.Exists(lastPath))
                {
                    return lastPath;
                }
            }

            // search directory
            string[] files = Directory.GetFiles("Assets", "Mirror.Runtime.dll", SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                // save path for next time, but only if it's a relative path.
                // we don't want to use another project's dlls for weaving, that
                // would be debugging hell.
                // (Directory.GetFiles should return relative paths)
                if (!Path.IsPathRooted(files[0]))
                {
                    EditorPrefs.SetString(key, files[0]);
                }
                else
                {
                    Debug.Log("Weaving doesn't cache path because it's absolute: " + files[0]);
                }

                return files[0];
            }

            return "";
        }
    }
}