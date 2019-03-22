// this class generates OnSerialize/OnDeserialize for SyncLists
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    static class SyncListProcessor
    {
        public static void Process(TypeDefinition td)
        {
            // find item type
            GenericInstanceType gt = (GenericInstanceType)td.BaseType;
            if (gt.GenericArguments.Count == 0)
            {
                Weaver.Error("SyncListProcessor no generic args");
                return;
            }
            TypeReference itemType = Weaver.CurrentAssembly.MainModule.ImportReference(gt.GenericArguments[0]);

            Weaver.DLog(td, "SyncListProcessor Start item:" + itemType.FullName);

            Weaver.ResetRecursionCount();
            MethodReference writeItemFunc = GenerateSerialization(td, itemType);
            if (Weaver.WeavingFailed)
            {
                return;
            }

            MethodReference readItemFunc = GenerateDeserialization(td, itemType);

            if (readItemFunc == null || writeItemFunc == null)
                return;

            Weaver.DLog(td, "SyncListProcessor Done");
        }

        // serialization of individual element
        static MethodReference GenerateSerialization(TypeDefinition td, TypeReference itemType)
        {
            Weaver.DLog(td, "  GenerateSerialization");
            foreach (var m in td.Methods)
            {
                if (m.Name == "SerializeItem")
                    return m;
            }

            MethodDefinition serializeFunc = new MethodDefinition("SerializeItem", MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            serializeFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkWriterType)));
            serializeFunc.Parameters.Add(new ParameterDefinition("item", ParameterAttributes.None, itemType));
            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();

            if (itemType.IsGenericInstance)
            {
                Weaver.Error("GenerateSerialization for " + Helpers.PrettyPrintType(itemType) + " failed. Struct passed into SyncList<T> can't have generic parameters");
                return null;
            }

            MethodReference writeFunc = Weaver.GetWriteFunc(itemType);
            if (writeFunc != null)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_2));
                serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
            }
            else
            {
                Weaver.Error("GenerateSerialization for " + td.Name + " unknown type [" + itemType + "/" + itemType.FullName + "]. [SyncList] member variables must be basic types.");
                return null;
            }
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            td.Methods.Add(serializeFunc);
            return serializeFunc;
        }

        static MethodReference GenerateDeserialization(TypeDefinition td, TypeReference itemType)
        {
            Weaver.DLog(td, "  GenerateDeserialization");
            foreach (var m in td.Methods)
            {
                if (m.Name == "DeserializeItem")
                    return m;
            }

            MethodDefinition deserializeFunction = new MethodDefinition("DeserializeItem", MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    itemType);

            deserializeFunction.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));

            ILProcessor serWorker = deserializeFunction.Body.GetILProcessor();

            MethodReference readerFunc = Weaver.GetReadFunc(itemType);
            if (readerFunc != null)
            {
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                serWorker.Append(serWorker.Create(OpCodes.Call, readerFunc));
                serWorker.Append(serWorker.Create(OpCodes.Ret));
            }
            else
            {
                Weaver.Error("GenerateDeserialization for " + td.Name + " unknown type [" + itemType + "]. [SyncList] member variables must be basic types.");
                return null;
            }

            td.Methods.Add(deserializeFunction);
            return deserializeFunction;
        }
    }
}
