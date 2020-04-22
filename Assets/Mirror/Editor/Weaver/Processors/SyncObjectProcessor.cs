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
        /// <param name="genericArgument">Which generic argument to serialize,  0 is the first one</param>
        /// <param name="baseType">the type that has generic arguments</param>
        /// <param name="serializeMethod">The name of the serialize method</param>
        /// <param name="deserializeMethod">The name of the deserialize method</param>
        public static void GenerateSerialization(TypeDefinition td, TypeReference itemType, string serializeMethod, string deserializeMethod)
        {
            Weaver.DLog(td, "SyncObjectProcessor Start item:" + itemType.FullName);

            MethodReference writeItemFunc = GenerateSerialization(serializeMethod, td, itemType);
            if (Weaver.WeavingFailed)
            {
                return;
            }

            MethodReference readItemFunc = GenerateDeserialization(deserializeMethod, td, itemType);

            if (readItemFunc == null || writeItemFunc == null)
                return;

            Weaver.DLog(td, "SyncObjectProcessor Done");
        }

        // serialization of individual element
        static MethodReference GenerateSerialization(string methodName, TypeDefinition td, TypeReference itemType)
        {
            Weaver.DLog(td, "  GenerateSerialization");
            MethodDefinition existing = td.GetMethod(methodName);
            if (existing != null)
                return existing;


            // this check needs to happen inside GenerateSerialization because
            // we need to check if user has made custom function above
            if (itemType.IsGenericInstance)
            {
                Weaver.Error($"{td} Can not create Serialize or Deserialize for generic element. Override virtual methods with custom Serialize and Deserialize to use {itemType} in SyncList");
                return null;
            }

            MethodDefinition serializeFunc = new MethodDefinition(methodName, MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            serializeFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkWriterType)));
            serializeFunc.Parameters.Add(new ParameterDefinition("item", ParameterAttributes.None, itemType));
            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();

            MethodReference writeFunc = Writers.GetWriteFunc(itemType);
            if (writeFunc != null)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_2));
                serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
            }
            else
            {
                Weaver.Error($"{td} cannot have item of type {itemType}.  Use a type supported by mirror instead");
                return null;
            }
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            td.Methods.Add(serializeFunc);
            return serializeFunc;
        }

        static MethodReference GenerateDeserialization(string methodName, TypeDefinition td, TypeReference itemType)
        {
            Weaver.DLog(td, "  GenerateDeserialization");
            MethodDefinition existing = td.GetMethod(methodName);
            if (existing != null)
                return existing;

            // this check needs to happen inside GenerateDeserialization because
            // we need to check if user has made custom function above
            if (itemType.IsGenericInstance)
            {
                Weaver.Error($"{td} Can not create Serialize or Deserialize for generic element. Override virtual methods with custom Serialize and Deserialize to use {itemType} in SyncList");
                return null;
            }

            MethodDefinition deserializeFunction = new MethodDefinition(methodName, MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    itemType);

            deserializeFunction.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));

            ILProcessor serWorker = deserializeFunction.Body.GetILProcessor();

            MethodReference readerFunc = Readers.GetReadFunc(itemType);
            if (readerFunc != null)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                serWorker.Append(serWorker.Create(OpCodes.Call, readerFunc));
                serWorker.Append(serWorker.Create(OpCodes.Ret));
            }
            else
            {
                Weaver.Error($"{td} cannot have item of type {itemType}.  Use a type supported by mirror instead");
                return null;
            }

            td.Methods.Add(deserializeFunction);
            return deserializeFunction;
        }
    }
}
