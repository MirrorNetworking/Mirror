using System.Collections.Generic;
using System.Threading.Tasks;
using AssetStoreTools.Uploader.Data;
using AssetStoreTools.Utility;
using AssetStoreTools.Utility.Json;

namespace AssetStoreTools.Uploader.Utility
{
    internal class PackageFetcher
    {
        public abstract class PackageFetcherResult
        {
            public bool Success;
            public bool SilentFail;
            public ASError Error;
            public JsonValue Json;
        }
        
        public class PackageFetcherResultSingle : PackageFetcherResult
        {
            public PackageData Package;
        }

        public class PackageFetcherResultCollection : PackageFetcherResult
        {
            public ICollection<PackageData> Packages;
        }

        public async Task<PackageFetcherResultCollection> Fetch(bool useCached)
        {
            var result = await AssetStoreAPI.GetFullPackageDataAsync(useCached);
            if (!result.Success)
                return new PackageFetcherResultCollection() { Success = false, Error = result.Error, SilentFail = result.SilentFail };

            if (result.Response.Equals(default(JsonValue)))
            {
                ASDebug.Log("No packages fetched");
                return new PackageFetcherResultCollection() { Success = true, Packages = null, Json = result.Response };
            }

            var packages = ParsePackages(result.Response);
            return new PackageFetcherResultCollection() { Success = true, Packages = packages, Json = result.Response };
        }

        public async Task<PackageFetcherResultSingle> FetchRefreshedPackage(string packageId)
        {
            var result = await AssetStoreAPI.GetRefreshedPackageData(packageId);
            if (!result.Success)
            {
                ASDebug.LogError(result.Error.Message);
                return new PackageFetcherResultSingle() { Success = false, Error = result.Error, SilentFail = result.SilentFail };
            }

            var package = ParseRefreshedPackage(packageId, result.Response);
            return new PackageFetcherResultSingle() { Success = true, Package = package };
        }

        private ICollection<PackageData> ParsePackages(JsonValue json)
        {
            List<PackageData> packageList = new List<PackageData>();
            
            var packageDict = json["packages"].AsDict(true);
            ASDebug.Log($"All packages\n{json["packages"]}");
            // Each package has an identifier and a bunch of data (current version id, name, etc.)
            foreach (var p in packageDict)
            {
                var packageId = p.Key;
                var package = ParseRefreshedPackage(packageId, p.Value);
                packageList.Add(package);
            }

            return packageList;
        }

        private PackageData ParseRefreshedPackage(string packageId, JsonValue json)
        {
            var packageName = json["name"].AsString(true);
            var versionId = json["id"].AsString(true);
            var statusName = json["status"].AsString(true);
            var isCompleteProject = json["is_complete_project"].AsBool(true);
            var categoryName = json["extra_info"].Get("category_info").Get("name").AsString(true);
            var lastUploadedPath = json["project_path"].IsString() ? json["project_path"].AsString() : string.Empty;
            var lastUploadedGuid = json["root_guid"].IsString() ? json["root_guid"].AsString() : string.Empty;

            var lastDate = json["extra_info"].Get("modified").AsString(true);
            var lastSize = json["extra_info"].Get("size").AsString(true);
            
            var package = new PackageData(packageId, packageName, versionId, statusName, categoryName, isCompleteProject, lastUploadedPath, lastUploadedGuid, lastDate, lastSize);
            ASDebug.Log(package);
            return package;
        }
    }
}