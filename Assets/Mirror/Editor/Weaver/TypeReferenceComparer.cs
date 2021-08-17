using System.Collections.Generic;
using Mono.CecilX;

namespace Mirror.Weaver
{
    // Compares TypeReference using FullName
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
