using System.Collections.Generic;
using Mono.CecilX;

namespace Mirror.Weaver
{
    public static class SyncObjectProcessor
    {
        // ulong = 64 bytes
        const int SyncObjectsLimit = 64;

        // Finds SyncObjects fields in a type
        // Type should be a NetworkBehaviour
        public static List<FieldDefinition> FindSyncObjectsFields(Writers writers, Readers readers, Logger Log, TypeDefinition td, ref bool WeavingFailed)
        {
            List<FieldDefinition> syncObjects = new List<FieldDefinition>();

            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.FieldType.Resolve().ImplementsInterface<SyncObject>())
                {
                    if (fd.IsStatic)
                    {
                        Log.Error($"{fd.Name} cannot be static", fd);
                        WeavingFailed = true;
                        continue;
                    }

                    GenerateReadersAndWriters(writers, readers, fd.FieldType, ref WeavingFailed);

                    syncObjects.Add(fd);
                }
            }

            // SyncObjects dirty mask is 64 bit. can't sync more than 64.
            if (syncObjects.Count > 64)
            {
                Log.Error($"{td.Name} has > {SyncObjectsLimit} SyncObjects (SyncLists etc). Consider refactoring your class into multiple components", td);
                WeavingFailed = true;
            }


            return syncObjects;
        }

        // Generates serialization methods for synclists
        static void GenerateReadersAndWriters(Writers writers, Readers readers, TypeReference tr, ref bool WeavingFailed)
        {
            if (tr is GenericInstanceType genericInstance)
            {
                foreach (TypeReference argument in genericInstance.GenericArguments)
                {
                    if (!argument.IsGenericParameter)
                    {
                        readers.GetReadFunc(argument, ref WeavingFailed);
                        writers.GetWriteFunc(argument, ref WeavingFailed);
                    }
                }
            }

            if (tr != null)
            {
                GenerateReadersAndWriters(writers, readers, tr.Resolve().BaseType, ref WeavingFailed);
            }
        }
    }
}
