using System.Collections.Generic;
using Mono.CecilX;

namespace Mirror.Weaver
{
    /// <summary>
    /// Comparers TypeReference using FullName
    /// </summary>
    public class TypeReferenceComparer : IEqualityComparer<TypeReference>
    {
        public bool Equals(TypeReference x, TypeReference y)
        {
            return x.FullName == y.FullName;
        }

        public int GetHashCode(TypeReference obj)
        {
            return obj.FullName.GetHashCode();
        }
    }
}
