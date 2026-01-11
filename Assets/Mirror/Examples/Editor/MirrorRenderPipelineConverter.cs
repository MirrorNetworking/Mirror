#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

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
            if (HasConvertedFlag())
                return;

            RenderPipelineType currentPipeline = DetectRenderPipeline();
            if (currentPipeline == RenderPipelineType.BuiltIn)
            {
                SetConvertedFlag();
                return;
            }

            string examplesPath = GetExamplesFolderPath();
            if (string.IsNullOrEmpty(examplesPath))
            {
                Debug.LogError("Could not locate Examples folder!");
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                $"Mirror Examples {currentPipeline} Conversion",
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
            return pipeline != null && pipeline.GetType().Name.Contains("Universal") ? RenderPipelineType.URP : RenderPipelineType.BuiltIn;
        }

        private static string GetExamplesFolderPath()
        {
            string[] guids = AssetDatabase.FindAssets("MirrorRenderPipelineConverter t:Script");
            if (guids.Length == 0) return null;
            string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return Path.GetDirectoryName(Path.GetDirectoryName(scriptPath))?.Replace("\\", "/");
        }

        /// <summary>
        /// Converts materials in the Examples folder to match the current render pipeline.
        /// Only processes .mat files and forces conversion for legacy/built-in shaders.
        /// </summary>
        private static void ConvertMaterials(RenderPipelineType targetPipeline, string examplesPath)
        {
            if (!Directory.Exists(examplesPath))
            {
                Debug.LogError($"Examples folder not found at: {examplesPath}");
                return;
            }

            try
            {
                // Find all .mat files in Examples folder and subfolders
                string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { examplesPath })
                    .Where(guid => AssetDatabase.GUIDToAssetPath(guid).EndsWith(".mat"))
                    .ToArray();

                foreach (string guid in materialGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                    if (mat != null)
                    {
                        string shaderName = mat.shader.name;

                        // Force conversion for non-URP shaders that won't render correctly
                        bool needsConversion = shaderName.StartsWith("Legacy Shaders/") ||
                                              shaderName == "Standard" ||
                                              shaderName == "Standard (Specular setup)";

                        if (!needsConversion && Shader.Find(shaderName) != null)
                        {
                            Debug.Log($"Shader '{shaderName}' exists and is assumed URP-compatible, skipping: {path}");
                            continue;
                        }

                        if (targetPipeline == RenderPipelineType.URP)
                            ConvertToURP(mat);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during material conversion: {e.Message}");
            }
        }

        /// <summary>
        /// Converts a material to URP-compatible shaders, preserving properties like color, texture,
        /// tiling, transparency, and forward rendering options (specular highlights and reflections).
        /// </summary>
        private static void ConvertToURP(Material material)
        {
            string originalShaderName = material.shader.name;

            // Capture all relevant properties before changing the shader
            Color initialColor = material.GetColor("_Color");
            Debug.Log($"Initial _Color for '{material.name}': {initialColor} (Hex: {ColorUtility.ToHtmlStringRGBA(initialColor)})", material);

            Texture mainTex = null;
            Vector4 mainTexST = Vector4.zero; // x, y = tiling; z, w = offset
            if (material.HasProperty("_MainTex"))
            {
                mainTex = material.GetTexture("_MainTex");
                mainTexST = material.GetVector("_MainTex_ST");
                Debug.Log($"Initial _MainTex for '{material.name}': {(mainTex != null ? mainTex.name : "null")}, Tiling/Offset: {mainTexST}", material);
            }
            else if (material.mainTexture != null)
            {
                mainTex = material.mainTexture;
                mainTexST = new Vector4(material.mainTextureScale.x, material.mainTextureScale.y, material.mainTextureOffset.x, material.mainTextureOffset.y);
                Debug.Log($"Initial mainTexture fallback for '{material.name}': {mainTex.name}, Tiling/Offset: {mainTexST}", material);
            }

            Texture bumpMap = null;
            if (material.HasProperty("_BumpMap"))
            {
                bumpMap = material.GetTexture("_BumpMap");
                Debug.Log($"Initial _BumpMap for '{material.name}': {(bumpMap != null ? bumpMap.name : "null")}", material);
            }

            float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0f;
            Debug.Log($"Initial Metallic for '{material.name}': {metallic}, Smoothness: {smoothness}", material);

            bool isTransparent = material.renderQueue == (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Capture Forward Rendering Options (Specular Highlights and Reflections)
            float specularHighlights = material.HasProperty("_SpecularHighlights") ? material.GetFloat("_SpecularHighlights") : 0f; // Default off
            float environmentReflections = material.HasProperty("_GlossyReflections") ? material.GetFloat("_GlossyReflections") : 0f; // Default off
            Debug.Log($"Initial Specular Highlights for '{material.name}': {(specularHighlights > 0 ? "On" : "Off")}, Environment Reflections: {(environmentReflections > 0 ? "On" : "Off")}", material);

            // Handle skybox materials
            if (originalShaderName.StartsWith("Skybox/"))
            {
                if (originalShaderName == "Skybox/6 Sided")
                {
                    Shader urpSixSidedShader = Shader.Find("Skybox/6 Sided");
                    if (urpSixSidedShader != null && material.shader != urpSixSidedShader)
                    {
                        material.shader = urpSixSidedShader;
                        Debug.Log($"Converted to URP Skybox/6 Sided: {AssetDatabase.GetAssetPath(material)}", material);
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
                        Debug.Log($"Converted to URP Skybox/Panoramic: {AssetDatabase.GetAssetPath(material)}", material);
                    }
                    return;
                }
            }

            // Convert to URP Lit for all other materials
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                Debug.LogError("URP Lit shader not found in project!");
                return;
            }

            material.shader = urpLitShader;

            // Apply texture and tiling
            if (mainTex != null)
            {
                material.SetTexture("_BaseMap", mainTex);
                material.SetVector("_BaseMap_ST", mainTexST);
                Debug.Log($"Set _BaseMap for '{material.name}' to: {mainTex.name}, Tiling/Offset: {mainTexST}", material);
            }
            else
            {
                Debug.Log($"No albedo texture found for '{material.name}' at {AssetDatabase.GetAssetPath(material)}", material);
            }

            // Apply color
            Color baseColor = initialColor == Color.clear ? Color.white : initialColor;
            material.SetColor("_BaseColor", baseColor);
            Debug.Log($"Set _BaseColor for '{material.name}' to: {baseColor} (Hex: {ColorUtility.ToHtmlStringRGBA(baseColor)})", material);

            // Apply normal map
            if (bumpMap != null)
            {
                material.SetTexture("_BumpMap", bumpMap);
                Debug.Log($"Set _BumpMap for '{material.name}' to: {bumpMap.name}", material);
            }

            // Apply metallic and smoothness
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            Debug.Log($"Set Metallic for '{material.name}' to: {metallic}, Smoothness to: {smoothness}", material);

            // Apply forward rendering options
            material.SetFloat("_SpecularHighlights", specularHighlights);
            material.SetFloat("_EnvironmentReflections", environmentReflections);
            Debug.Log($"Set Specular Highlights for '{material.name}' to: {(specularHighlights > 0 ? "On" : "Off")}, Environment Reflections to: {(environmentReflections > 0 ? "On" : "Off")}", material);

            // Set surface type and blending
            if (isTransparent)
            {
                material.SetFloat("_Surface", 1f); // Transparent
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                Debug.Log($"Set '{material.name}' to Transparent Surface Type", material);
            }
            else
            {
                material.SetFloat("_Surface", 0f); // Opaque
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                Debug.Log($"Set '{material.name}' to Opaque Surface Type", material);
            }

            Debug.Log($"Converted '{originalShaderName}' to URP Lit: {AssetDatabase.GetAssetPath(material)}", material);
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
#endif
