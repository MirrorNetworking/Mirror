using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{

    public static class Writers
    {
        const int MaxRecursionCount = 128;

        static Dictionary<string, MethodReference> writeFuncs;

        public static void Init(AssemblyDefinition CurrentAssembly)
        {
            TypeReference networkWriterType = Weaver.NetworkWriterType;

            writeFuncs = new Dictionary<string, MethodReference>
            {
                { Weaver.singleType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.singleType) },
                { Weaver.doubleType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.doubleType) },
                { Weaver.boolType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.boolType) },
                { Weaver.stringType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.stringType) },
                { Weaver.int64Type.FullName, Resolvers.ResolveMethod(networkWriterType, CurrentAssembly, "WritePackedInt64") },
                { Weaver.uint64Type.FullName, Weaver.NetworkWriterWritePackedUInt64 },
                { Weaver.int32Type.FullName, Resolvers.ResolveMethod(networkWriterType, CurrentAssembly, "WritePackedInt32") },
                { Weaver.uint32Type.FullName, Resolvers.ResolveMethod(networkWriterType, CurrentAssembly, "WritePackedUInt32") },
                { Weaver.int16Type.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.int16Type) },
                { Weaver.uint16Type.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.uint16Type) },
                { Weaver.byteType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.byteType) },
                { Weaver.sbyteType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.sbyteType) },
                { Weaver.charType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.charType) },
                { Weaver.decimalType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.decimalType) },
                { Weaver.vector2Type.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.vector2Type) },
                { Weaver.vector3Type.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.vector3Type) },
                { Weaver.vector4Type.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.vector4Type) },
                { Weaver.vector2IntType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.vector2IntType) },
                { Weaver.vector3IntType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.vector3IntType) },
                { Weaver.colorType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.colorType) },
                { Weaver.color32Type.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.color32Type) },
                { Weaver.quaternionType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.quaternionType) },
                { Weaver.rectType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.rectType) },
                { Weaver.planeType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.planeType) },
                { Weaver.rayType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.rayType) },
                { Weaver.matrixType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.matrixType) },
                { Weaver.guidType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.guidType) },
                { Weaver.gameObjectType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.gameObjectType) },
                { Weaver.NetworkIdentityType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.NetworkIdentityType) },
                { Weaver.transformType.FullName, Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "Write", Weaver.transformType) },
                { "System.Byte[]", Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "WriteBytesAndSize", "System.Byte[]") },
                { "System.ArraySegment`1<System.Byte>", Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "WriteBytesAndSizeSegment", "System.ArraySegment`1<System.Byte>") }
            };
        }

        public static MethodReference GetWriteFunc(TypeReference variable, int recursionCount = 0)
        {
            if (writeFuncs.TryGetValue(variable.FullName, out MethodReference foundFunc))
            {
                if (foundFunc.Parameters[0].ParameterType.IsArray == variable.IsArray)
                {
                    return foundFunc;
                }
            }

            if (variable.IsByReference)
            {
                // error??
                Weaver.Error($"{variable} has unsupported type. Use one of Mirror supported types instead");
                return null;
            }

            MethodDefinition newWriterFunc;

            if (variable.IsArray)
            {
                newWriterFunc = GenerateArrayWriteFunc(variable, recursionCount);
            }
            else if (variable.Resolve().IsEnum)
            {
                return GetWriteFunc(variable.Resolve().GetEnumUnderlyingType(), recursionCount);
            }
            else
            {
                newWriterFunc = GenerateStructWriterFunction(variable, recursionCount);
            }

            if (newWriterFunc == null)
            {
                return null;
            }

            RegisterWriteFunc(variable.FullName, newWriterFunc);
            return newWriterFunc;
        }

        static void RegisterWriteFunc(string name, MethodDefinition newWriterFunc)
        {
            writeFuncs[name] = newWriterFunc;
            Weaver.WeaveLists.generatedWriteFunctions.Add(newWriterFunc);

            Weaver.ConfirmGeneratedCodeClass();
            Weaver.WeaveLists.generateContainerClass.Methods.Add(newWriterFunc);
        }

        static MethodDefinition GenerateStructWriterFunction(TypeReference variable, int recursionCount)
        {
            if (recursionCount > MaxRecursionCount)
            {
                Weaver.Error($"{variable} can't be serialized because it references itself");
                return null;
            }

            if (!Weaver.IsValidTypeToGenerate(variable.Resolve()))
            {
                return null;
            }

            string functionName = "_Write" + variable.Name + "_";
            if (variable.DeclaringType != null)
            {
                functionName += variable.DeclaringType.Name;
            }
            else
            {
                functionName += "None";
            }
            // create new writer for this type
            MethodDefinition writerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            writerFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkWriterType)));
            writerFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(variable)));

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            uint fields = 0;
            foreach (FieldDefinition field in variable.Resolve().Fields)
            {
                if (field.IsStatic || field.IsPrivate)
                    continue;

                if (field.FieldType.Resolve().HasGenericParameters)
                {
                    Weaver.Error($"{field} has unsupported type. Create a derived class instead of using generics");
                    return null;
                }

                if (field.FieldType.Resolve().IsInterface)
                {
                    Weaver.Error($"{field} has unsupported type. Use a concrete class instead of an interface");
                    return null;
                }

                MethodReference writeFunc = GetWriteFunc(field.FieldType, recursionCount + 1);
                if (writeFunc != null)
                {
                    fields++;
                    worker.Append(worker.Create(OpCodes.Ldarg_0));
                    worker.Append(worker.Create(OpCodes.Ldarg_1));
                    worker.Append(worker.Create(OpCodes.Ldfld, field));
                    worker.Append(worker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error($"{field} has unsupported type. Use a type supported by Mirror instead");
                    return null;
                }
            }
            if (fields == 0)
            {
                Log.Warning($" {variable} has no no public or non-static fields to serialize");
            }
            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }

        static MethodDefinition GenerateArrayWriteFunc(TypeReference variable, int recursionCount)
        {

            if (!variable.IsArrayType())
            {
                Weaver.Error($"{variable} is an unsupported type. Jagged and multidimensional arrays are not supported");
                return null;
            }

            TypeReference elementType = variable.GetElementType();
            MethodReference elementWriteFunc = GetWriteFunc(elementType, recursionCount + 1);
            if (elementWriteFunc == null)
            {
                return null;
            }

            string functionName = "_WriteArray" + variable.GetElementType().Name + "_";
            if (variable.DeclaringType != null)
            {
                functionName += variable.DeclaringType.Name;
            }
            else
            {
                functionName += "None";
            }

            // create new writer for this type
            MethodDefinition writerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            writerFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkWriterType)));
            writerFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(variable)));

            writerFunc.Body.Variables.Add(new VariableDefinition(Weaver.int32Type));
            writerFunc.Body.Variables.Add(new VariableDefinition(Weaver.int32Type));
            writerFunc.Body.InitLocals = true;

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            // if (value == null)
            // {
            //     writer.WritePackedInt32(-1);
            //     return;
            // }
            Instruction labelNull = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Brtrue, labelNull));

            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4_M1));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkWriterWritePackedInt32));
            worker.Append(worker.Create(OpCodes.Ret));

            // int length = value.Length;
            worker.Append(labelNull);
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Ldlen));
            worker.Append(worker.Create(OpCodes.Stloc_0));

            // writer.WritePackedInt32(length);
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkWriterWritePackedInt32));

            // for (int i=0; i< value.length; i++) {
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Stloc_1));
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Br, labelHead));

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);
            // writer.Write(value[i]);
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldelema, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Ldobj, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Call, elementWriteFunc));


            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldc_I4_1));
            worker.Append(worker.Create(OpCodes.Add));
            worker.Append(worker.Create(OpCodes.Stloc_1));


            // end for loop
            worker.Append(labelHead);
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Ldlen));
            worker.Append(worker.Create(OpCodes.Conv_I4));
            worker.Append(worker.Create(OpCodes.Blt, labelBody));

            // return
            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }
    }
}
