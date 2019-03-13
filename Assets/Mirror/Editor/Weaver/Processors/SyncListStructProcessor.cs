// this class generates OnSerialize/OnDeserialize for SyncListStructs
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    static class SyncListStructProcessor
    {
        public static void Process(TypeDefinition td)
        {
            // find item type
            GenericInstanceType gt = (GenericInstanceType)td.BaseType;
            if (gt.GenericArguments.Count == 0)
            {
                Weaver.Error("SyncListStructProcessor no generic args");
                return;
            }
            TypeReference itemType = Weaver.CurrentAssembly.MainModule.ImportReference(gt.GenericArguments[0]);

            Weaver.DLog(td, "SyncListStructProcessor Start item:" + itemType.FullName);

            Weaver.ResetRecursionCount();
            MethodReference writeItemFunc = GenerateSerialization(td, itemType);
            if (Weaver.WeavingFailed)
            {
                return;
            }

            MethodReference readItemFunc = GenerateDeserialization(td, itemType);

            if (readItemFunc == null || writeItemFunc == null)
                return;

            Weaver.DLog(td, "SyncListStructProcessor Done");
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
                Weaver.Error("GenerateSerialization for " + Helpers.PrettyPrintType(itemType) + " failed. Struct passed into SyncListStruct<T> can't have generic parameters");
                return null;
            }

            foreach (FieldDefinition field in itemType.Resolve().Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                FieldReference importedField = Weaver.CurrentAssembly.MainModule.ImportReference(field);
                TypeDefinition ft = importedField.FieldType.Resolve();

                if (ft.HasGenericParameters)
                {
                    Weaver.Error("GenerateSerialization for " + td.Name + " [" + ft + "/" + ft.FullName + "]. [SyncListStruct] member cannot have generic parameters.");
                    return null;
                }

                if (ft.IsInterface)
                {
                    Weaver.Error("GenerateSerialization for " + td.Name + " [" + ft + "/" + ft.FullName + "]. [SyncListStruct] member cannot be an interface.");
                    return null;
                }

                MethodReference writeFunc = Weaver.GetWriteFunc(field.FieldType);
                if (writeFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_2));
                    serWorker.Append(serWorker.Create(OpCodes.Ldfld, importedField));
                    serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error("GenerateSerialization for " + td.Name + " unknown type [" + ft + "/" + ft.FullName + "]. [SyncListStruct] member variables must be basic types.");
                    return null;
                }
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

            MethodDefinition serializeFunc = new MethodDefinition("DeserializeItem", MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    itemType);

            serializeFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));

            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();

            serWorker.Body.InitLocals = true;
            serWorker.Body.Variables.Add(new VariableDefinition(itemType));

            // init item instance
            serWorker.Append(serWorker.Create(OpCodes.Ldloca, 0));
            serWorker.Append(serWorker.Create(OpCodes.Initobj, itemType));

            foreach (FieldDefinition field in itemType.Resolve().Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                FieldReference importedField = Weaver.CurrentAssembly.MainModule.ImportReference(field);
                TypeDefinition ft = importedField.FieldType.Resolve();

                MethodReference readerFunc = Weaver.GetReadFunc(field.FieldType);
                if (readerFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Ldloca, 0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                    serWorker.Append(serWorker.Create(OpCodes.Call, readerFunc));
                    serWorker.Append(serWorker.Create(OpCodes.Stfld, importedField));
                }
                else
                {
                    Weaver.Error("GenerateDeserialization for " + td.Name + " unknown type [" + ft + "]. [SyncListStruct] member variables must be basic types.");
                    return null;
                }
            }
            serWorker.Append(serWorker.Create(OpCodes.Ldloc_0));
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            td.Methods.Add(serializeFunc);
            return serializeFunc;
        }
    }
}
