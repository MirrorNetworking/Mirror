using AssetStoreTools.Utility.Json;
using System.IO;
using System.Text;
using UnityEngine;

namespace AssetStoreTools.Uploader.Utility
{
    internal static class AssetStoreCache
    {
        public const string TempCachePath = "Temp/AssetStoreToolsCache";
        public const string PersistentCachePath = "Library/AssetStoreToolsCache";

        private const string PackageDataFile = "PackageMetadata.json";
        private const string CategoryDataFile = "Categories.json";

        private static void CreateFileInTempCache(string fileName, object content, bool overwrite)
        {
            CreateCacheFile(TempCachePath, fileName, content, overwrite);
        }

        private static void CreateFileInPersistentCache(string fileName, object content, bool overwrite)
        {
            CreateCacheFile(PersistentCachePath, fileName, content, overwrite);
        }

        private static void CreateCacheFile(string rootPath, string fileName, object content, bool overwrite)
        {
            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            var fullPath = Path.Combine(rootPath, fileName);

            if(File.Exists(fullPath))
            {
                if (overwrite)
                    File.Delete(fullPath);
                else
                    return;
            }

            switch (content)
            {
                case byte[] bytes:
                    File.WriteAllBytes(fullPath, bytes);
                    break;
                default:
                    File.WriteAllText(fullPath, content.ToString());
                    break;
            }
        }

        public static void ClearTempCache()
        {
            if (!File.Exists(Path.Combine(TempCachePath, PackageDataFile)))
                return;
            
            // Cache consists of package data and package texture thumbnails. We don't clear
            // texture thumbnails here since they are less likely to change. They are still
            // deleted and redownloaded every project restart (because of being stored in the 'Temp' folder)
            File.Delete(Path.Combine(TempCachePath, PackageDataFile));
        }

        public static void CacheCategories(JsonValue data)
        {
            CreateFileInTempCache(CategoryDataFile, data, true);
        }

        public static bool GetCachedCategories(out JsonValue data)
        {
            data = new JsonValue();
            var path = Path.Combine(TempCachePath, CategoryDataFile);
            if (!File.Exists(path))
                return false;

            data = JSONParser.SimpleParse(File.ReadAllText(path, Encoding.UTF8));
            return true;
        }

        public static void CachePackageMetadata(JsonValue data)
        {
            CreateFileInTempCache(PackageDataFile, data.ToString(), true);
        }

        public static bool GetCachedPackageMetadata(out JsonValue data)
        {
            data = new JsonValue();
            var path = Path.Combine(TempCachePath, PackageDataFile);
            if (!File.Exists(path))
                return false;

            data = JSONParser.SimpleParse(File.ReadAllText(path, Encoding.UTF8));
            return true;
        }

        public static void CacheTexture(string packageId, Texture2D texture)
        {
            CreateFileInTempCache($"{packageId}.png", texture.EncodeToPNG(), true);
        }

        public static bool GetCachedTexture(string packageId, out Texture2D texture)
        {
            texture = new Texture2D(1, 1);
            var path = Path.Combine(TempCachePath, $"{packageId}.png");
            if (!File.Exists(path))
                return false;

            texture.LoadImage(File.ReadAllBytes(path));
            return true;
        }

        public static void CacheUploadSelections(string packageId, JsonValue json)
        {
            var fileName = $"{packageId}-uploadselection.asset";
            CreateFileInPersistentCache(fileName, json.ToString(), true);
        }

        public static bool GetCachedUploadSelections(string packageId, out JsonValue json)
        {
            json = new JsonValue();
            var path = Path.Combine(PersistentCachePath, $"{packageId}-uploadselection.asset");
            if (!File.Exists(path))
                return false;

            json = JSONParser.SimpleParse(File.ReadAllText(path, Encoding.UTF8));
            return true;
        }
    }
}
