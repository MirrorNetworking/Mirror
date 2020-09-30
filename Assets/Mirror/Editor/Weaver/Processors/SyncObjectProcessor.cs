using System.Collections.Generic;
using Mono.Cecil;

namespace Mirror.Weaver
{
    public static class SyncObjectProcessor
    {
        /// <summary>
        /// Finds SyncObjects fields in a type
        /// <para>Type should be a NetworkBehaviour</para>
        /// </summary>
        /// <param name="td"></param>
        /// <returns></returns>
        public static List<FieldDefinition> FindSyncObjectsFields(TypeDefinition td)
        {
            var syncObjects = new List<FieldDefinition>();

            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.FieldType.Resolve().ImplementsInterface<ISyncObject>())
                {
                    if (fd.IsStatic)
                    {
                        Weaver.Error($"{fd.Name} cannot be static", fd);
                        continue;
                    }

                    syncObjects.Add(fd);
                }
            }

            return syncObjects;
        }
    }
}
