// this class generates OnSerialize/OnDeserialize when inheriting from MessageBase
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    static class MessageClassProcessor
    {
        public static void Process(TypeDefinition td)
        {
            Weaver.DLog(td, "MessageClassProcessor Start");

            GenerateSerialization(td);
            if (Weaver.WeavingFailed)
            {
                return;
            }

            GenerateDeSerialization(td);
            Weaver.DLog(td, "MessageClassProcessor Done");
        }

        static void GenerateSerialization(TypeDefinition td)
        {
            Weaver.DLog(td, "  GenerateSerialization");
            foreach (MethodDefinition m in td.Methods)
            {
                if (m.Name == "Serialize")
                    return;
            }

            if (td.Fields.Count == 0)
            {
                return;
            }

            // check for self-referencing types
            foreach (FieldDefinition field in td.Fields)
            {
                if (field.FieldType.FullName == td.FullName)
                {
                    Weaver.Error("GenerateSerialization for " + td.Name + " [" + field.FullName + "]. [MessageBase] member cannot be self referencing.");
                    return;
                }
            }

            MethodDefinition serializeFunc = new MethodDefinition("Serialize",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    Weaver.voidType);

            serializeFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkWriterType)));
            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();

            foreach (FieldDefinition field in td.Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                if (field.FieldType.Resolve().HasGenericParameters)
                {
                    Weaver.Error("GenerateSerialization for " + td.Name + " [" + field.FieldType + "/" + field.FieldType.FullName + "]. [MessageBase] member cannot have generic parameters.");
                    return;
                }

                if (field.FieldType.Resolve().IsInterface)
                {
                    Weaver.Error("GenerateSerialization for " + td.Name + " [" + field.FieldType + "/" + field.FieldType.FullName + "]. [MessageBase] member cannot be an interface.");
                    return;
                }

                MethodReference writeFunc = Writers.GetWriteFunc(field.FieldType);
                if (writeFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldfld, field));
                    serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error("GenerateSerialization for " + td.Name + " unknown type [" + field.FieldType + "/" + field.FieldType.FullName + "]. [MessageBase] member variables must be basic types.");
                    return;
                }
            }
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            td.Methods.Add(serializeFunc);
        }

        static void GenerateDeSerialization(TypeDefinition td)
        {
            Weaver.DLog(td, "  GenerateDeserialization");
            foreach (MethodDefinition m in td.Methods)
            {
                if (m.Name == "Deserialize")
                    return;
            }

            if (td.Fields.Count == 0)
            {
                return;
            }

            MethodDefinition serializeFunc = new MethodDefinition("Deserialize",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    Weaver.voidType);

            serializeFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));
            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();

            foreach (FieldDefinition field in td.Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                MethodReference readerFunc = Readers.GetReadFunc(field.FieldType);
                if (readerFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                    serWorker.Append(serWorker.Create(OpCodes.Call, readerFunc));
                    serWorker.Append(serWorker.Create(OpCodes.Stfld, field));
                }
                else
                {
                    Weaver.Error("GenerateDeSerialization for " + td.Name + " unknown type [" + field.FieldType + "]. [SyncVar] member variables must be basic types.");
                    return;
                }
            }
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            td.Methods.Add(serializeFunc);
        }
    }
}
