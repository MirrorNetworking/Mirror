// this class generates OnSerialize/OnDeserialize for SyncListStructs
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    class SyncListStructProcessor
    {
        TypeDefinition m_TypeDef;
        TypeReference m_ItemType;

        public SyncListStructProcessor(TypeDefinition typeDef)
        {
            Weaver.DLog(typeDef, "SyncListStructProcessor for " + typeDef.Name);
            m_TypeDef = typeDef;
        }

        public void Process()
        {
            // find item type
            var gt = (GenericInstanceType)m_TypeDef.BaseType;
            if (gt.GenericArguments.Count == 0)
            {
                Weaver.fail = true;
                Log.Error("SyncListStructProcessor no generic args");
                return;
            }
            m_ItemType = Weaver.scriptDef.MainModule.ImportReference(gt.GenericArguments[0]);

            Weaver.DLog(m_TypeDef, "SyncListStructProcessor Start item:" + m_ItemType.FullName);

            Weaver.ResetRecursionCount();
            var writeItemFunc = GenerateSerialization();
            if (Weaver.fail)
            {
                return;
            }

            var readItemFunc = GenerateDeserialization();

            if (readItemFunc == null || writeItemFunc == null)
                return;

            Weaver.DLog(m_TypeDef, "SyncListStructProcessor Done");
        }

        // serialization of individual element
        MethodReference GenerateSerialization()
        {
            Weaver.DLog(m_TypeDef, "  GenerateSerialization");
            foreach (var m in m_TypeDef.Methods)
            {
                if (m.Name == "SerializeItem")
                    return m;
            }

            MethodDefinition serializeFunc = new MethodDefinition("SerializeItem", MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            serializeFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.scriptDef.MainModule.ImportReference(Weaver.NetworkWriterType)));
            serializeFunc.Parameters.Add(new ParameterDefinition("item", ParameterAttributes.None, m_ItemType));
            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();

            if (m_ItemType.IsGenericInstance)
            {
                Weaver.fail = true;
                Log.Error("GenerateSerialization for " + Helpers.PrettyPrintType(m_ItemType) + " failed. Struct passed into SyncListStruct<T> can't have generic parameters");
                return null;
            }

            foreach (var field in m_ItemType.Resolve().Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                var importedField = Weaver.scriptDef.MainModule.ImportReference(field);
                var ft = importedField.FieldType.Resolve();

                if (ft.HasGenericParameters)
                {
                    Weaver.fail = true;
                    Log.Error("GenerateSerialization for " + m_TypeDef.Name + " [" + ft + "/" + ft.FullName + "]. UNet [MessageBase] member cannot have generic parameters.");
                    return null;
                }

                if (ft.IsInterface)
                {
                    Weaver.fail = true;
                    Log.Error("GenerateSerialization for " + m_TypeDef.Name + " [" + ft + "/" + ft.FullName + "]. UNet [MessageBase] member cannot be an interface.");
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
                    Weaver.fail = true;
                    Log.Error("GenerateSerialization for " + m_TypeDef.Name + " unknown type [" + ft + "/" + ft.FullName + "]. UNet [MessageBase] member variables must be basic types.");
                    return null;
                }
            }
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            m_TypeDef.Methods.Add(serializeFunc);
            return serializeFunc;
        }

        MethodReference GenerateDeserialization()
        {
            Weaver.DLog(m_TypeDef, "  GenerateDeserialization");
            foreach (var m in m_TypeDef.Methods)
            {
                if (m.Name == "DeserializeItem")
                    return m;
            }

            MethodDefinition serializeFunc = new MethodDefinition("DeserializeItem", MethodAttributes.Public |
                    MethodAttributes.Virtual |
                    MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    m_ItemType);

            serializeFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.scriptDef.MainModule.ImportReference(Weaver.NetworkReaderType)));

            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();

            serWorker.Body.InitLocals = true;
            serWorker.Body.Variables.Add(new VariableDefinition(m_ItemType));

            // init item instance
            serWorker.Append(serWorker.Create(OpCodes.Ldloca, 0));
            serWorker.Append(serWorker.Create(OpCodes.Initobj, m_ItemType));

            foreach (var field in m_ItemType.Resolve().Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                var importedField = Weaver.scriptDef.MainModule.ImportReference(field);
                var ft = importedField.FieldType.Resolve();

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
                    Weaver.fail = true;
                    Log.Error("GenerateDeserialization for " + m_TypeDef.Name + " unknown type [" + ft + "]. UNet [SyncVar] member variables must be basic types.");
                    return null;
                }
            }
            serWorker.Append(serWorker.Create(OpCodes.Ldloc_0));
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            m_TypeDef.Methods.Add(serializeFunc);
            return serializeFunc;
        }
    }
}
