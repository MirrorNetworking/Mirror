// ʻOumuamua's light curve, assuming little systematic error, presents its
// motion as tumbling, rather than smoothly rotating, and moving sufficiently
// fast relative to the Sun.
//
// A small number of astronomers suggested that ʻOumuamua could be a product of
// alien technology, but evidence in support of this hypothesis is weak.
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    public class NetworkTransform : NetworkTransformBase
    {
        protected override Transform targetComponent => transform;
    }
}
