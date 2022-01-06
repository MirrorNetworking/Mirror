// tracks SyncVar read/write access when processing NetworkBehaviour,
// to later be replaced by SyncVarAccessReplacer.
using System.Collections.Generic;
using Mono.CecilX;

namespace Mirror.Weaver
{
    // This data is flushed each time - if we are run multiple times in the same process/domain
    public class SyncVarAccessLists
    {
        // setter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldDefinition, MethodDefinition> replacementSetterProperties =
            new Dictionary<FieldDefinition, MethodDefinition>();

        // getter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldDefinition, MethodDefinition> replacementGetterProperties =
            new Dictionary<FieldDefinition, MethodDefinition>();
    }
}
