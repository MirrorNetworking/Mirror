using System.Collections.Generic;
using Mono.CecilX;

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
            List<FieldDefinition> syncObjects = new List<FieldDefinition>();

            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.FieldType.Resolve().ImplementsInterface<SyncObject>())
                {
                    if (fd.IsStatic)
                    {
                        Weaver.Error($"{fd.Name} cannot be static", fd);
                        continue;
                    }

                    GenerateReadersAndWriters(fd.FieldType);

                    syncObjects.Add(fd);
                }
            }


            return syncObjects;
        }

        /// <summary>
        /// Generates serialization methods for synclists
        /// </summary>
        /// <param name="td">The synclist class</param>
        /// <param name="mirrorBaseType">the base SyncObject td inherits from</param>
        static void GenerateReadersAndWriters(TypeReference tr)
        {
            if (tr is GenericInstanceType genericInstance)
            {
                foreach (TypeReference argument in genericInstance.GenericArguments)
                {
                    if (!argument.IsGenericParameter)
                    {
                        Readers.GetReadFunc(argument);
                        Writers.GetWriteFunc(argument);
                    }
                }
            }

            if (tr != null)
            {
                GenerateReadersAndWriters(tr.Resolve().BaseType);
            }
        }
    }
}
