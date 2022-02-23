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
                if (fd.FieldType.IsGenericParameter)
                {
                    // can't call .Resolve on generic ones
                    continue;
                }

                if (fd.FieldType.Resolve().IsDerivedFrom<SyncObject>())
                {
                    if (fd.IsStatic)
                    {
                        Log.Error($"{fd.Name} cannot be static", fd);
                        WeavingFailed = true;
                        continue;
                    }

                    // SyncObjects always needs to be readonly to guarantee.
                    // Weaver calls InitSyncObject on them for dirty bits etc.
                    // Reassigning at runtime would cause undefined behaviour.
                    // (C# 'readonly' is called 'initonly' in IL code.)
                    //
                    // NOTE: instead of forcing readonly, we could also scan all
                    //       instructions for SyncObject assignments. this would
                    //       make unit tests very difficult though.
                    if (!fd.IsInitOnly)
                    {
                        // just a warning for now.
                        // many people might still use non-readonly SyncObjects.
                        Log.Warning($"{fd.Name} should have a 'readonly' keyword in front of the variable because {typeof(SyncObject)}s always need to be initialized by the Weaver.", fd);

                        // only log, but keep weaving. no need to break projects.
                        //WeavingFailed = true;
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
