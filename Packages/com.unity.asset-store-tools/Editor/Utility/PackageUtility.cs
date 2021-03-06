#if UNITY_2019 || UNITY_2020
using System;
using System.Reflection;
#endif
using UnityEngine;
using UnityEditor.PackageManager;
using System.Linq;

namespace AssetStoreTools.Utility
{
    internal static class PackageUtility
    {
        /// <summary>
        /// Returns the package path on disk. If the path is within the root
        /// project folder, the returned path will be relative to the root project folder.
        /// Otherwise, an absolute path is returned
        /// </summary>
        public static string GetConvenientPath(this PackageInfo packageInfo)
        {
            var path = packageInfo.resolvedPath.Replace("\\", "/");

            var rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            if (path.StartsWith(rootProjectPath))
                path = path.Substring(rootProjectPath.Length);

            return path;
        }

        public static PackageInfo[] GetAllPackages()
        {
#if UNITY_2019 || UNITY_2020
            var method = typeof(PackageInfo).GetMethod("GetAll", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[0], null);
            var packages = method?.Invoke(null, null) as PackageInfo[];
#else
            var packages = PackageInfo.GetAllRegisteredPackages();
#endif
            return packages;
        }

        public static PackageInfo[] GetAllLocalPackages()
        {
            var packages = GetAllPackages();
            var localPackages = packages.Where(x => x.source == PackageSource.Embedded || x.source == PackageSource.Local)
                .Where(x => x.isDirectDependency).ToArray();
            return localPackages;
        }

        public static PackageInfo[] GetAllRegistryPackages()
        {
            var packages = GetAllPackages();
            var registryPackages = packages.Where(x => x.source == PackageSource.Registry || x.source == PackageSource.BuiltIn)
                .Where(x => x.isDirectDependency).ToArray();
            return registryPackages;
        }
    }
}