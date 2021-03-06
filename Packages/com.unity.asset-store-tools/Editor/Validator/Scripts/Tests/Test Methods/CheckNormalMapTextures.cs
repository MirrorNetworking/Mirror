using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckNormalMapTextures : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var materials = AssetUtility.GetObjectsFromAssets<Material>(config.ValidationPaths, AssetType.Material);
            var badTextures = new List<Texture>();

            foreach (var mat in materials)
            {
                for (int i = 0; i < mat.shader.GetPropertyCount(); i++)
                {
                    if ((mat.shader.GetPropertyFlags(i) & UnityEngine.Rendering.ShaderPropertyFlags.Normal) != 0)
                    {
                        var propertyName = mat.shader.GetPropertyName(i);
                        var assignedTexture = mat.GetTexture(propertyName);

                        if (assignedTexture == null)
                            continue;

                        var texturePath = AssetUtility.ObjectToAssetPath(assignedTexture);
                        var textureImporter = (TextureImporter)AssetUtility.GetAssetImporter(texturePath);
                        if (textureImporter.textureType != TextureImporterType.NormalMap && !badTextures.Contains(assignedTexture))
                            badTextures.Add(assignedTexture);
                    }
                }
            }

            if (badTextures.Count == 0)
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("All normal map textures have the correct texture type!");
            }
            else
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("The following textures are not set to type 'Normal Map'", null, badTextures.ToArray());
            }

            return result;
        }
    }
}
