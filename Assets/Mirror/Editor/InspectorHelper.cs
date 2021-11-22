using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Mirror
{
    public static class InspectorHelper
    {
        /// <summary>Gets all public and private fields for a type</summary>
        // deepestBaseType: Stops at this base type (exclusive)
        public static IEnumerable<FieldInfo> GetAllFields(Type type, Type deepestBaseType)
        {
            const BindingFlags publicFields = BindingFlags.Public | BindingFlags.Instance;
            const BindingFlags privateFields = BindingFlags.NonPublic | BindingFlags.Instance;

            // get public fields (includes fields from base type)
            FieldInfo[] allPublicFields = type.GetFields(publicFields);
            foreach (FieldInfo field in allPublicFields)
            {
                yield return field;
            }

            // get private fields in current type, then move to base type
            while (type != null)
            {
                FieldInfo[] allPrivateFields = type.GetFields(privateFields);
                foreach (FieldInfo field in allPrivateFields)
                {
                    yield return field;
                }

                type = type.BaseType;

                // stop early
                if (type == deepestBaseType)
                {
                    break;
                }
            }
        }

        public static bool IsSyncVar(this FieldInfo field)
        {
            object[] fieldMarkers = field.GetCustomAttributes(typeof(SyncVarAttribute), true);
            return fieldMarkers.Length > 0;
        }

        public static bool IsSerializeField(this FieldInfo field)
        {
            object[] fieldMarkers = field.GetCustomAttributes(typeof(SerializeField), true);
            return fieldMarkers.Length > 0;
        }

        public static bool IsVisibleField(this FieldInfo field)
        {
            return field.IsPublic || IsSerializeField(field);
        }

        public static bool ImplementsInterface<T>(this FieldInfo field)
        {
            return typeof(T).IsAssignableFrom(field.FieldType);
        }

        public static bool HasShowInInspector(this FieldInfo field)
        {
            object[] fieldMarkers = field.GetCustomAttributes(typeof(ShowInInspectorAttribute), true);
            return fieldMarkers.Length > 0;
        }

        // checks if SyncObject is public or has our custom [ShowInInspector] field
        public static bool IsVisibleSyncObject(this FieldInfo field)
        {
            return field.IsPublic || HasShowInInspector(field);
        }
    }
}
