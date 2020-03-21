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
        public string path;
        [SerializeField] string assetGuid;

        public bool HasValue()
        {
            return !string.IsNullOrEmpty(path);
        }

        public bool IsActiveScene()
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            return activeScene.path == path;
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
