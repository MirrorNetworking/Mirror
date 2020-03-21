using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    /// <summary>
    /// Used for Scene property in the inspector
    /// </summary>
    [System.Serializable]
    public struct SceneField : IEquatable<SceneField>
    {
        [SerializeField] string path;
        [SerializeField] string assetGuid;

        public string Path => path; 

        public bool HasValue()
        {
            return !string.IsNullOrEmpty(Path);
        }

        public bool IsActiveScene()
        {
            if (string.IsNullOrEmpty(Path))
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            return activeScene.path == Path;
        }

        public override bool Equals(object obj)
        {
            return obj is SceneField field && Equals(field);
        }

        public bool Equals(SceneField other)
        {
            return assetGuid == other.assetGuid;
        }

        public override int GetHashCode()
        {
            return 1488015982 + EqualityComparer<string>.Default.GetHashCode(assetGuid);
        }

        public static bool operator ==(SceneField left, SceneField right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SceneField left, SceneField right)
        {
            return !(left == right);
        }
    }
}
