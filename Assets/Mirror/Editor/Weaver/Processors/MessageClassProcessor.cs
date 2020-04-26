// this class generates OnSerialize/OnDeserialize when inheriting from MessageBase

using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    static class MessageClassProcessor
    {

        static bool IsEmptyDefault(this MethodBody body)
        {
            return body.Instructions.All(instruction => instruction.OpCode == OpCodes.Nop || instruction.OpCode == OpCodes.Ret);
        }

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
            MethodDefinition existingMethod = td.GetMethod("Serialize");
            if (existingMethod != null && !existingMethod.Body.IsEmptyDefault())
            {
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
                    Weaver.Error($"{td.Name} has field {field.Name} that references itself", field);
                    return;
                }
            }

            MethodDefinition serializeFunc = existingMethod ?? new MethodDefinition("Serialize",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    Weaver.voidType);

            //only add to new method
            if (existingMethod == null)
            {
                serializeFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkWriterType)));
            }
            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();
            if (existingMethod != null)
            {
                //remove default nop&ret from existing empty interface method
                serWorker.Body.Instructions.Clear();
            }

            // if it is not a struct, call base
            if (!td.IsValueType)
            {
                // call base
                CallBase(td, serWorker, "Serialize");
            }

            foreach (FieldDefinition field in td.Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                CallWriter(serWorker, field);
            }
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            //only add if not just replaced body
            if (existingMethod == null)
            {
                td.Methods.Add(serializeFunc);
            }
        }

        static void CallWriter(ILProcessor serWorker, FieldDefinition field)
        {
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
                Weaver.Error($"{field.Name} has unsupported type", field);
            }
        }

        static void CallBase(TypeDefinition td, ILProcessor serWorker, string name)
        {
            MethodReference method = Resolvers.ResolveMethodInParents(td.BaseType, Weaver.CurrentAssembly, name);
            if (method != null)
            {
                // base
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                // writer
                serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                serWorker.Append(serWorker.Create(OpCodes.Call, method));
            }
        }

        static void GenerateDeSerialization(TypeDefinition td)
        {
            Weaver.DLog(td, "  GenerateDeserialization");
            MethodDefinition existingMethod = td.GetMethod("Deserialize");
            if (existingMethod != null && !existingMethod.Body.IsEmptyDefault())
            {
                return;
            }

            if (td.Fields.Count == 0)
            {
                return;
            }

            MethodDefinition serializeFunc = existingMethod ?? new MethodDefinition("Deserialize",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    Weaver.voidType);

            //only add to new method
            if (existingMethod == null)
            {
                serializeFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));
            }
            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();
            if (existingMethod != null)
            {
                //remove default nop&ret from existing empty interface method
                serWorker.Body.Instructions.Clear();
            }

            // if not value type, call base
            if (!td.IsValueType)
            {
                CallBase(td, serWorker, "Deserialize");
            }

            foreach (FieldDefinition field in td.Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                CallReader(serWorker, field);
            }
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            //only add if not just replaced body
            if (existingMethod == null)
            {
                td.Methods.Add(serializeFunc);
            }
        }

        static void CallReader(ILProcessor serWorker, FieldDefinition field)
        {
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
                Weaver.Error($"{field.Name} has unsupported type", field);
            }
        }
    }
}
