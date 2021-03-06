using AssetStoreTools.Uploader.Data;
using AssetStoreTools.Uploader.UIElements;
using System.Collections.Generic;

namespace AssetStoreTools.Uploader.Utility
{
    internal static class PackageViewStorer
    {
        private static readonly Dictionary<string, PackageView> SavedPackages = new Dictionary<string, PackageView>();
        
        public static PackageView GetPackage(PackageData packageData)
        {
            string versionId = packageData.VersionId;
            if (SavedPackages.ContainsKey(versionId))
            {
                var savedPackage = SavedPackages[versionId];
                savedPackage.UpdateDataValues(packageData);
                return savedPackage;
            }

            var package = new PackageView(packageData);
            SavedPackages.Add(package.VersionId, package);
            return package;
        }

        public static void Reset()
        {
            SavedPackages.Clear();
        }
    }
}