using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Used for Scene property in the inspector
    /// </summary>
    [System.Serializable]
    public struct SceneField
    {
        public string path;
        [SerializeField] string assetGuid;
    }
}
