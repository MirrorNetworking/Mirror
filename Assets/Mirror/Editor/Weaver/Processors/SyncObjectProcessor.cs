using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public static class SyncObjectProcessor
    {
        /// <summary>
        /// Generates the serialization and deserialization methods for a specified generic argument
        /// </summary>
        /// <param name="td">The type of the class that needs serialization methods</param>
        /// <param name="itemType">generic argument to serialize</param>
        /// <param name="mirrorBaseType">the base SyncObject td inherits from</param>
        /// <param name="serializeMethod">The name of the serialize method</param>
        /// <param name="deserializeMethod">The name of the deserialize method</param>
        /// <exception cref="GenerateWriterException">Throws when writer could not be generated for itemType</exception>
        /// <exception cref="SyncObjectException">Throws when Serialization functions could not be created</exception>
        public static void GenerateSerialization(TypeDefinition td, TypeReference itemType, TypeReference mirrorBaseType, string serializeMethod, string deserializeMethod)
        {
            Weaver.DLog(td, "SyncObjectProcessor Start item:" + itemType.FullName);

            GenerateSerialization(serializeMethod, td, itemType, mirrorBaseType);
            bool success = GenerateDeserialization(deserializeMethod, td, itemType, mirrorBaseType);

            if (success)
                Weaver.DLog(td, "SyncObjectProcessor Done");
        }

        /// <exception cref="GenerateWriterException">Throws when writer could not be generated for itemType</exception>
        /// <exception cref="SyncObjectException">Throws when Serialization functions could not be created</exception>
        static void GenerateSerialization(string methodName, TypeDefinition td, TypeReference itemType, TypeReference mirrorBaseType)
        {
            Weaver.DLog(td, "  GenerateSerialization");
            bool existing = td.HasMethodInBaseType(methodName, mirrorBaseType);
            if (existing)
                return;


            // this check needs to happen inside GenerateSerialization because
            // we need to check if user has made custom function above
            if (itemType.IsGenericInstance)
            {
                throw new SyncObjectException($"Can not create Serialize or Deserialize for generic element in {td.Name}. Override virtual methods with custom Serialize and Deserialize to use {itemType} in SyncList", td);
            }

            MethodDefinition serializeFunc = new MethodDefinition(methodName, MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    WeaverTypes.voidType);

            serializeFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(WeaverTypes.NetworkWriterType)));
            serializeFunc.Parameters.Add(new ParameterDefinition("item", ParameterAttributes.None, itemType));
            ILProcessor worker = serializeFunc.Body.GetILProcessor();

            MethodReference writeFunc = Writers.GetWriteFunc(itemType);

            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Ldarg_2));
            worker.Append(worker.Create(OpCodes.Call, writeFunc));

            worker.Append(worker.Create(OpCodes.Ret));

            td.Methods.Add(serializeFunc);
        }

        /// <exception cref="SyncObjectException">Throws when Serialization functions could not be created</exception>
        static bool GenerateDeserialization(string methodName, TypeDefinition td, TypeReference itemType, TypeReference mirrorBaseType)
        {
            Weaver.DLog(td, "  GenerateDeserialization");
            bool existing = td.HasMethodInBaseType(methodName, mirrorBaseType);
            if (existing)
                return true;

            // this check needs to happen inside GenerateDeserialization because
            // we need to check if user has made custom function above
            if (itemType.IsGenericInstance)
            {
                throw new SyncObjectException($"Can not create Serialize or Deserialize for generic element in {td.Name}. Override virtual methods with custom Serialize and Deserialize to use {itemType.Name} in SyncList", td);
            }

            MethodDefinition deserializeFunction = new MethodDefinition(methodName, MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    itemType);

            deserializeFunction.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(WeaverTypes.NetworkReaderType)));

            ILProcessor worker = deserializeFunction.Body.GetILProcessor();

            MethodReference readerFunc = Readers.GetReadFunc(itemType);
            if (readerFunc != null)
            {
                worker.Append(worker.Create(OpCodes.Ldarg_1));
                worker.Append(worker.Create(OpCodes.Call, readerFunc));
                worker.Append(worker.Create(OpCodes.Ret));
            }
            else
            {
                Weaver.Error($"{td.Name} has sync object generic type {itemType.Name}.  Use a type supported by mirror instead", td);
                return false;
            }

            td.Methods.Add(deserializeFunction);
            return true;
        }
    }
}
