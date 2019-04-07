using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Mirror.Weaver
{

    public class Writers
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
                { "System.Byte[]", Resolvers.ResolveMethodWithArg(networkWriterType, CurrentAssembly, "WriteBytesAndSize", "System.Byte[]") }
            };
        }

        public static MethodReference GetWriteFunc(TypeReference variable, int recursionCount = 0)
        {
            if (recursionCount > MaxRecursionCount)
            {
                Weaver.Error("GetWriteFunc recursion depth exceeded for " + variable.Name + ". Check for self-referencing member variables.");
                return null;
            }

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
                Weaver.Error("GetWriteFunc variable.IsByReference error.");
                return null;
            }

            MethodDefinition newWriterFunc;

            if (variable.IsArray)
            {
                TypeReference elementType = variable.GetElementType();
                MethodReference elemenWriteFunc = GetWriteFunc(elementType, recursionCount + 1);
                if (elemenWriteFunc == null)
                {
                    return null;
                }
                newWriterFunc = GenerateArrayWriteFunc(variable, elemenWriteFunc);
            }
            else
            {
                if (variable.Resolve().IsEnum)
                {
                    return Weaver.NetworkWriterWriteInt32;
                }

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
                    Weaver.Error("WriteReadFunc for " + field.Name + " [" + field.FieldType + "/" + field.FieldType.FullName + "]. Cannot have generic parameters.");
                    return null;
                }

                if (field.FieldType.Resolve().IsInterface)
                {
                    Weaver.Error("WriteReadFunc for " + field.Name + " [" + field.FieldType + "/" + field.FieldType.FullName + "]. Cannot be an interface.");
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
                    Weaver.Error("WriteReadFunc for " + field.Name + " type " + field.FieldType + " no supported");
                    return null;
                }
            }
            if (fields == 0)
            {
                Log.Warning("The class / struct " + variable.Name + " has no public or non-static fields to serialize");
            }
            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }

        static MethodDefinition GenerateArrayWriteFunc(TypeReference variable, MethodReference elementWriteFunc)
        {
            if (!variable.IsArrayType())
            {
                Log.Error(variable.FullName + " is an unsupported array type. Jagged and multidimensional arrays are not supported");
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

            writerFunc.Body.Variables.Add(new VariableDefinition(Weaver.uint16Type));
            writerFunc.Body.Variables.Add(new VariableDefinition(Weaver.uint16Type));
            writerFunc.Body.InitLocals = true;

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            // null check
            Instruction labelNull = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Brtrue, labelNull));

            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkWriteUInt16));
            worker.Append(worker.Create(OpCodes.Ret));

            // setup array length local variable
            worker.Append(labelNull);
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Ldlen));
            worker.Append(worker.Create(OpCodes.Conv_I4));
            worker.Append(worker.Create(OpCodes.Conv_U2));
            worker.Append(worker.Create(OpCodes.Stloc_0));

            //write length
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkWriteUInt16));

            // start loop
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Stloc_1));
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Br, labelHead));

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldelema, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Ldobj, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Call, elementWriteFunc));
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldc_I4_1));
            worker.Append(worker.Create(OpCodes.Add));
            worker.Append(worker.Create(OpCodes.Conv_U2));
            worker.Append(worker.Create(OpCodes.Stloc_1));

            // loop while check
            worker.Append(labelHead);
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Ldlen));
            worker.Append(worker.Create(OpCodes.Conv_I4));
            worker.Append(worker.Create(OpCodes.Blt, labelBody));

            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }
    }
}