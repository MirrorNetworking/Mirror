using UnityEngine;

namespace Mirror.Cloud
{
    /// <summary>
    /// Adds Extension to check if unity object is null.
    /// <para>Use these methods to stop MissingReferenceException</para>
    /// </summary>
    public interface IUnityEqualCheck
    {

    }

    public static class UnityEqualCheckExtension
    {
        public static bool IsNull(this IUnityEqualCheck obj)
        {
            return (obj as Object) == null;
        }

        public static bool IsNotNull(this IUnityEqualCheck obj)
        {
            return (obj as Object) != null;
        }
    }
}
