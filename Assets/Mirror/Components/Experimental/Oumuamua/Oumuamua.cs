using UnityEngine;

namespace Mirror.Experimental
{
    [DisallowMultipleComponent]
    public class Oumuamua : OumuamuaBase
    {
        protected override Transform targetComponent => transform;
    }
}
