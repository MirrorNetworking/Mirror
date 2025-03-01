using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Mirror.Examples.Editor
{
    [InitializeOnLoad]
    public class MirrorRenderPipelineConverter
    {
        private const string CONVERTED_FLAG_FILE = "MirrorExamplesPipelineConverted.txt";

        static MirrorRenderPipelineConverter()
        {
            EditorApplication.delayCall += CheckPipelineOnLoad;
        }

        private static void CheckPipelineOnLoad()
        {
            // Check if already converted
            if (HasConvertedFlag())
                return;

            // Detect current pipeline
            RenderPipelineType currentPipeline = DetectRenderPipeline();
            if (currentPipeline == RenderPipelineType.BuiltIn)
            {
                // No conversion needed, silently set flag and exit
                SetConvertedFlag();
                return;
            }

            // Get Examples folder path dynamically
            string examplesPath = GetExamplesFolderPath();
            if (string.IsNullOrEmpty(examplesPath))
            {
                Debug.LogError("Could not locate Examples folder!");
                return;
            }

            // Show dialog with three options
            int choice = EditorUtility.DisplayDialogComplex(
                "Mirror Examples Pipeline Conversion",
                $"Mirror examples need to be converted to {currentPipeline}.\n\nThis will only affect {examplesPath} and subfolders.",
                "Go Ahead",         // 0 - Left (confirm)
                "Don't Ask Again",  // 1 - Right (cancel)
                "Not Now"           // 2 - Middle (alternative)
);

            switch (choice)
            {
                case 0: // Go Ahead
                    ConvertMaterials(currentPipeline, examplesPath);
                    SetConvertedFlag();
                    Debug.Log("Mirror Examples materials converted to " + currentPipeline);
                    break;
                case 1: // Don't Ask Again
                    SetConvertedFlag();
                    break;
                case 2: // Not Now
                    // Do nothing
                    break;
            }
        }

        private enum RenderPipelineType
        {
            BuiltIn,
            URP
        }

        private static RenderPipelineType DetectRenderPipeline()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
                return RenderPipelineType.BuiltIn;

            if (pipeline.GetType().Name.Contains("Universal"))
                return RenderPipelineType.URP;

            return RenderPipelineType.BuiltIn; // Default fallback
        }

        private static string GetExamplesFolderPath()
        {
            // Find this script's path by searching for its type
            string[] guids = AssetDatabase.FindAssets("MirrorRenderPipelineConverter t:Script");
            if (guids.Length == 0)
                return null;

            string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            // Go up one level from Editor folder to Examples folder
            string examplesPath = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath));

            return examplesPath?.Replace("\\", "/"); // Ensure forward slashes for Unity
        }

        private static void ConvertMaterials(RenderPipelineType targetPipeline, string examplesPath)
        {
            if (!Directory.Exists(examplesPath))
            {
                Debug.LogError("Examples folder not found at: " + examplesPath);
                return;
            }

            try
            {
                string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { examplesPath })
                    .Where(guid => AssetDatabase.GUIDToAssetPath(guid).EndsWith(".mat"))
                    .ToArray();

                foreach (string guid in materialGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                    if (mat != null)
                    {
                        // Force conversion for non-URP shaders
                        string shaderName = mat.shader.name;
                        bool needsConversion = shaderName.StartsWith("Legacy Shaders/") ||
                                              shaderName == "Standard" ||
                                              shaderName == "Standard (Specular setup)";

                        if (!needsConversion && Shader.Find(shaderName) != null)
                        {
                            Debug.Log($"Shader {shaderName} exists and is assumed URP-compatible, skipping: {path}");
                            continue;
                        }

                        if (targetPipeline == RenderPipelineType.URP)
                        {
                            ConvertToURP(mat);
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error during material conversion: " + e.Message);
            }
        }

        private static void ConvertToURP(Material material)
        {
            string originalShaderName = material.shader.name;

            // Capture initial state before any changes
            Color initialColor = material.GetColor("_Color");
            Debug.Log($"Initial _Color for {material.name}: {initialColor} (Hex: {ColorUtility.ToHtmlStringRGBA(initialColor)})");

            Texture initialMainTex = null;
            Vector4 initialMainTex_ST = Vector4.zero; // For tiling/offset (x, y = scale; z, w = offset)
            if (material.HasProperty("_MainTex"))
            {
                initialMainTex = material.GetTexture("_MainTex");
                initialMainTex_ST = material.GetVector("_MainTex_ST"); // Tiling and offset
                Debug.Log($"Initial _MainTex for {material.name}: {(initialMainTex != null ? initialMainTex.name : "null")}, Tiling/Offset: {initialMainTex_ST}");
            }
            if (initialMainTex == null && material.mainTexture != null)
            {
                initialMainTex = material.mainTexture;
                initialMainTex_ST = new Vector4(material.mainTextureScale.x, material.mainTextureScale.y, material.mainTextureOffset.x, material.mainTextureOffset.y);
                Debug.Log($"Initial mainTexture fallback for {material.name}: {initialMainTex.name}, Tiling/Offset: {initialMainTex_ST}");
            }

            Texture initialBumpMap = null;
            if (material.HasProperty("_BumpMap"))
            {
                initialBumpMap = material.GetTexture("_BumpMap");
                Debug.Log($"Initial _BumpMap for {material.name}: {(initialBumpMap != null ? initialBumpMap.name : "null")}");
            }

            float initialMetallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            float initialGlossiness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
            Debug.Log($"Initial Metallic for {material.name}: {initialMetallic}, Glossiness: {initialGlossiness}");

            // Capture rendering mode (0 = Opaque, 1 = Transparent, etc.)
            int renderQueue = material.renderQueue;
            bool isTransparent = renderQueue == (int)UnityEngine.Rendering.RenderQueue.Transparent; // 3000 typically indicates Transparent

            // Handle skybox conversion
            if (originalShaderName.StartsWith("Skybox/"))
            {
                if (originalShaderName == "Skybox/6 Sided")
                {
                    Shader urpSixSidedShader = Shader.Find("Skybox/6 Sided");
                    if (urpSixSidedShader != null && material.shader != urpSixSidedShader)
                    {
                        material.shader = urpSixSidedShader;
                        Debug.Log($"Updated to URP Skybox/6 Sided: {AssetDatabase.GetAssetPath(material)}");
                    }
                    return;
                }
                else
                {
                    Shader urpSkyboxShader = Shader.Find("Skybox/Panoramic");
                    if (urpSkyboxShader != null && material.shader != urpSkyboxShader)
                    {
                        material.shader = urpSkyboxShader;
                        if (material.HasProperty("_Tex"))
                            material.SetTexture("_MainTex", material.GetTexture("_Tex"));
                        else if (material.HasProperty("_FrontTex"))
                            material.SetTexture("_MainTex", material.GetTexture("_FrontTex"));
                        if (material.HasProperty("_Tint"))
                            material.SetColor("_Tint", material.GetColor("_Tint"));
                        Debug.Log($"Converted to URP Skybox/Panoramic: {AssetDatabase.GetAssetPath(material)}");
                    }
                    return;
                }
            }

            // Convert to URP Lit
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                Debug.LogError("URP Lit shader not found in project!");
                return;
            }

            material.shader = urpLitShader;

            // Apply captured albedo texture and tiling
            if (initialMainTex != null)
            {
                material.SetTexture("_BaseMap", initialMainTex);
                material.SetVector("_BaseMap_ST", initialMainTex_ST); // Set tiling and offset
                Debug.Log($"Set _BaseMap for {material.name} to: {initialMainTex.name}, Tiling/Offset: {initialMainTex_ST}");
            }
            else
            {
                Debug.Log($"No albedo texture found for {material.name} at {AssetDatabase.GetAssetPath(material)}");
            }

            // Apply captured color
            Color baseColor = initialColor;
            if (baseColor == Color.clear)
            {
                baseColor = material.color != Color.clear ? material.color : Color.white;
                Debug.Log($"Initial color was clear, using fallback for {material.name}: {baseColor} (Hex: {ColorUtility.ToHtmlStringRGBA(baseColor)})");
            }
            else
            {
                Debug.Log($"Using initial _Color for {material.name}: {baseColor} (Hex: {ColorUtility.ToHtmlStringRGBA(baseColor)})");
            }
            material.SetColor("_BaseColor", baseColor);

            // Apply captured normal map
            if (initialBumpMap != null)
            {
                material.SetTexture("_BumpMap", initialBumpMap);
                Debug.Log($"Set _BumpMap for {material.name} to: {initialBumpMap.name}");
            }

            // Apply metallic and smoothness
            material.SetFloat("_Metallic", initialMetallic);
            material.SetFloat("_Smoothness", initialGlossiness);
            Debug.Log($"Set Metallic for {material.name} to: {initialMetallic}, Smoothness to: {initialGlossiness}");

            // Apply surface type (rendering mode)
            if (isTransparent)
            {
                material.SetFloat("_Surface", 1f); // 1 = Transparent
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0); // Typically off for transparent
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                Debug.Log($"Set {material.name} to Transparent Surface Type");
            }
            else
            {
                material.SetFloat("_Surface", 0f); // 0 = Opaque
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1); // Typically on for opaque
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                Debug.Log($"Set {material.name} to Opaque Surface Type");
            }

            Debug.Log($"Converted {originalShaderName} to URP Lit: {AssetDatabase.GetAssetPath(material)}");
        }

        private static bool HasConvertedFlag()
        {
            string flagPath = Path.Combine(Application.dataPath, "..", CONVERTED_FLAG_FILE);
            return File.Exists(flagPath);
        }

        private static void SetConvertedFlag()
        {
            string flagPath = Path.Combine(Application.dataPath, "..", CONVERTED_FLAG_FILE);
            File.WriteAllText(flagPath, "Converted: " + System.DateTime.Now.ToString());
            AssetDatabase.Refresh();
        }
    }
}