using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirror
{
    public static class MirrorExtensions
    {
        /// <summary>
        /// Returns the path of the Transform in the hierarchy
        /// </summary>
        /// <param name="go">The Game Object to get the path of</param>
        /// <returns>The path of the object in scene Hierarchy. IE: World/Building/Window/Glass</returns>
        public static string GetHierarchyPath(this GameObject go)
        {
            Transform transform = go.transform;
            List<string> paths = new List<string>
            {
                transform.name
            };
            while (transform.parent != null)
            {
                transform = transform.parent;
                paths.Add(transform.name);
            }
            paths.Reverse();
            return string.Join("/", paths);
        }
    }
}
