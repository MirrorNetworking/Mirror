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
    public struct ScenePath : IEquatable<ScenePath>
    {
        [SerializeField] string path;

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
            return obj is ScenePath field && Equals(field);
        }

        public bool Equals(ScenePath other)
        {
            return path == other.path;
        }

        public override int GetHashCode()
        {
            return 1488015982 + EqualityComparer<string>.Default.GetHashCode(path);
        }

        public static bool operator ==(ScenePath left, ScenePath right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScenePath left, ScenePath right)
        {
            return !(left == right);
        }
    }
}
