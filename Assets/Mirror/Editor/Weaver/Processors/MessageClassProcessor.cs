using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    /// <summary>
    /// generates OnSerialize/OnDeserialize when inheriting from MessageBase
    /// </summary>
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
            MethodDefinition existingMethod = td.GetMethodWith1Arg("Serialize", WeaverTypes.NetworkWriterType);
            // do nothing if method exists and is abstract or not empty
            if (existingMethod != null && (existingMethod.IsAbstract || !existingMethod.Body.IsEmptyDefault()))
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
                    WeaverTypes.voidType);

            //only add to new method
            if (existingMethod == null)
            {
                serializeFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(WeaverTypes.NetworkWriterType)));
            }
            ILProcessor worker = serializeFunc.Body.GetILProcessor();
            if (existingMethod != null)
            {
                //remove default nop&ret from existing empty interface method
                worker.Body.Instructions.Clear();
            }

            // if it is not a struct, call base
            if (!td.IsValueType)
            {
                // call base
                CallBase(td, worker, "Serialize");
            }

            foreach (FieldDefinition field in td.Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                CallWriter(worker, field);
            }
            worker.Append(worker.Create(OpCodes.Ret));

            //only add if not just replaced body
            if (existingMethod == null)
            {
                td.Methods.Add(serializeFunc);
            }
        }

        static void CallWriter(ILProcessor worker, FieldDefinition field)
        {
            MethodReference writeFunc = Writers.GetWriteFunc(field.FieldType);
            if (writeFunc != null)
            {
                worker.Append(worker.Create(OpCodes.Ldarg_1));
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldfld, field));
                worker.Append(worker.Create(OpCodes.Call, writeFunc));
            }
            else
            {
                Weaver.Error($"{field.Name} has unsupported type", field);
            }
        }

        static void CallBase(TypeDefinition td, ILProcessor worker, string name)
        {
            MethodReference method = Resolvers.TryResolveMethodInParents(td.BaseType, Weaver.CurrentAssembly, name);

            // dont call method if it is null or abstract
            if (method == null || method.Resolve().IsAbstract)
            {
                return;
            }

            // base
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            // writer
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Call, method));
        }

        static void GenerateDeSerialization(TypeDefinition td)
        {
            Weaver.DLog(td, "  GenerateDeserialization");
            MethodDefinition existingMethod = td.GetMethodWith1Arg("Deserialize", WeaverTypes.NetworkReaderType);

            // do nothing if method exists and is abstract or not empty
            if (existingMethod != null && (existingMethod.IsAbstract || !existingMethod.Body.IsEmptyDefault()))
            {
                return;
            }

            if (td.Fields.Count == 0)
            {
                return;
            }

            MethodDefinition serializeFunc = existingMethod ?? new MethodDefinition("Deserialize",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    WeaverTypes.voidType);

            //only add to new method
            if (existingMethod == null)
            {
                serializeFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(WeaverTypes.NetworkReaderType)));
            }
            ILProcessor worker = serializeFunc.Body.GetILProcessor();
            if (existingMethod != null)
            {
                //remove default nop&ret from existing empty interface method
                worker.Body.Instructions.Clear();
            }

            // if not value type, call base
            if (!td.IsValueType)
            {
                CallBase(td, worker, "Deserialize");
            }

            foreach (FieldDefinition field in td.Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                CallReader(worker, field);
            }
            worker.Append(worker.Create(OpCodes.Ret));

            //only add if not just replaced body
            if (existingMethod == null)
            {
                td.Methods.Add(serializeFunc);
            }
        }

        static void CallReader(ILProcessor worker, FieldDefinition field)
        {
            MethodReference readerFunc = Readers.GetReadFunc(field.FieldType);
            if (readerFunc != null)
            {
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldarg_1));
                worker.Append(worker.Create(OpCodes.Call, readerFunc));
                worker.Append(worker.Create(OpCodes.Stfld, field));
            }
            else
            {
                Weaver.Error($"{field.Name} has unsupported type", field);
            }
        }
    }
}
