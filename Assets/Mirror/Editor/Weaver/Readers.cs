using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Mirror.Weaver
{
    public static class Readers
    {
        const int MaxRecursionCount = 128;
        static Dictionary<string, MethodReference> readFuncs;

        public static void Init(AssemblyDefinition CurrentAssembly)
        {
            TypeReference networkReaderType = Weaver.NetworkReaderType;

            readFuncs = new Dictionary<string, MethodReference>
            {
                { Weaver.singleType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadSingle") },
                { Weaver.doubleType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadDouble") },
                { Weaver.boolType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadBoolean") },
                { Weaver.stringType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadString") },
                { Weaver.int64Type.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadPackedInt64") },
                { Weaver.uint64Type.FullName, Weaver.NetworkReaderReadPackedUInt64 },
                { Weaver.int32Type.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadPackedInt32") },
                { Weaver.uint32Type.FullName, Weaver.NetworkReaderReadPackedUInt32 },
                { Weaver.int16Type.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadInt16") },
                { Weaver.uint16Type.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadUInt16") },
                { Weaver.byteType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadByte") },
                { Weaver.sbyteType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadSByte") },
                { Weaver.charType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadChar") },
                { Weaver.decimalType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadDecimal") },
                { Weaver.vector2Type.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadVector2") },
                { Weaver.vector3Type.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadVector3") },
                { Weaver.vector4Type.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadVector4") },
                { Weaver.vector2IntType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadVector2Int") },
                { Weaver.vector3IntType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadVector3Int") },
                { Weaver.colorType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadColor") },
                { Weaver.color32Type.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadColor32") },
                { Weaver.quaternionType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadQuaternion") },
                { Weaver.rectType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadRect") },
                { Weaver.planeType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadPlane") },
                { Weaver.rayType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadRay") },
                { Weaver.matrixType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadMatrix4x4") },
                { Weaver.guidType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadGuid") },
                { Weaver.gameObjectType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadGameObject") },
                { Weaver.NetworkIdentityType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadNetworkIdentity") },
                { Weaver.transformType.FullName, Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadTransform") },
                { "System.Byte[]", Resolvers.ResolveMethod(networkReaderType, CurrentAssembly, "ReadBytesAndSize") },
            };
        }


        public static MethodReference GetReadFunc(TypeReference variable, int recursionCount = 0)
        {
            if (recursionCount > MaxRecursionCount)
            {
                Weaver.Error("GetReadFunc recursion depth exceeded for " + variable.Name + ". Check for self-referencing member variables.");
                return null;
            }

            if (readFuncs.TryGetValue(variable.FullName, out MethodReference foundFunc))
            {
                if (foundFunc.ReturnType.IsArray == variable.IsArray)
                {
                    return foundFunc;
                }
            }

            TypeDefinition td = variable.Resolve();
            if (td == null)
            {
                Weaver.Error("GetReadFunc unsupported type " + variable.FullName);
                return null;
            }

            if (variable.IsByReference)
            {
                // error??
                Weaver.Error("GetReadFunc variable.IsByReference error.");
                return null;
            }

            MethodDefinition newReaderFunc;

            if (variable.IsArray)
            {
                TypeReference elementType = variable.GetElementType();
                MethodReference elementReadFunc = GetReadFunc(elementType, recursionCount + 1);
                if (elementReadFunc == null)
                {
                    return null;
                }
                newReaderFunc = GenerateArrayReadFunc(variable, elementReadFunc);
            }
            else
            {
                if (td.IsEnum)
                {
                    return Weaver.NetworkReaderReadInt32;
                }

                newReaderFunc = GenerateStructReadFunction(variable, recursionCount);
            }

            if (newReaderFunc == null)
            {
                Weaver.Error("GetReadFunc unable to generate function for:" + variable.FullName);
                return null;
            }
            RegisterReadFunc(variable.FullName, newReaderFunc);
            return newReaderFunc;
        }

        static void RegisterReadFunc(string name, MethodDefinition newReaderFunc)
        {
            readFuncs[name] = newReaderFunc;
            Weaver.WeaveLists.generatedReadFunctions.Add(newReaderFunc);

            Weaver.ConfirmGeneratedCodeClass();
            Weaver.WeaveLists.generateContainerClass.Methods.Add(newReaderFunc);
        }

        static MethodDefinition GenerateArrayReadFunc(TypeReference variable, MethodReference elementReadFunc)
        {
            if (!variable.IsArrayType())
            {
                Weaver.Error(variable.FullName + " is an unsupported array type. Jagged and multidimensional arrays are not supported");
                return null;
            }
            string functionName = "_ReadArray" + variable.GetElementType().Name + "_";
            if (variable.DeclaringType != null)
            {
                functionName += variable.DeclaringType.Name;
            }
            else
            {
                functionName += "None";
            }

            // create new reader for this type
            MethodDefinition readerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    variable);

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));

            readerFunc.Body.Variables.Add(new VariableDefinition(Weaver.int32Type));
            readerFunc.Body.Variables.Add(new VariableDefinition(variable));
            readerFunc.Body.Variables.Add(new VariableDefinition(Weaver.int32Type));
            readerFunc.Body.InitLocals = true;

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, Weaver.NetworkReadUInt16));
            worker.Append(worker.Create(OpCodes.Stloc_0));
            worker.Append(worker.Create(OpCodes.Ldloc_0));

            Instruction labelEmptyArray = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Brtrue, labelEmptyArray));

            // return empty array
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Newarr, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Ret));

            // create the actual array
            worker.Append(labelEmptyArray);
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Newarr, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Stloc_1));
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Stloc_2));

            // loop start
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Br, labelHead));

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldloc_2));
            worker.Append(worker.Create(OpCodes.Ldelema, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, elementReadFunc));
            worker.Append(worker.Create(OpCodes.Stobj, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Ldloc_2));
            worker.Append(worker.Create(OpCodes.Ldc_I4_1));
            worker.Append(worker.Create(OpCodes.Add));
            worker.Append(worker.Create(OpCodes.Stloc_2));

            // loop while check
            worker.Append(labelHead);
            worker.Append(worker.Create(OpCodes.Ldloc_2));
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Blt, labelBody));

            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        static MethodDefinition GenerateStructReadFunction(TypeReference variable, int recursionCount)
        {

            if (!Weaver.IsValidTypeToGenerate(variable.Resolve()))
            {
                return null;
            }

            string functionName = "_Read" + variable.Name + "_";
            if (variable.DeclaringType != null)
            {
                functionName += variable.DeclaringType.Name;
            }
            else
            {
                functionName += "None";
            }

            // create new reader for this type
            MethodDefinition readerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    variable);

            // create local for return value
            readerFunc.Body.Variables.Add(new VariableDefinition(variable));
            readerFunc.Body.InitLocals = true;

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            if (variable.IsValueType)
            {
                // structs are created with Initobj
                worker.Append(worker.Create(OpCodes.Ldloca, 0));
                worker.Append(worker.Create(OpCodes.Initobj, variable));
            }
            else
            {
                // classes are created with their constructor

                MethodDefinition ctor = Resolvers.ResolveDefaultPublicCtor(variable);
                if (ctor == null)
                {
                    Weaver.Error("The class " + variable.Name + " has no default constructor or it's private, aborting.");
                    return null;
                }

                worker.Append(worker.Create(OpCodes.Newobj, ctor));
                worker.Append(worker.Create(OpCodes.Stloc_0));
            }

            uint fields = 0;
            foreach (FieldDefinition field in variable.Resolve().Fields)
            {
                if (field.IsStatic || field.IsPrivate)
                    continue;

                // mismatched ldloca/ldloc for struct/class combinations is invalid IL, which causes crash at runtime
                OpCode opcode = variable.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc;
                worker.Append(worker.Create(opcode, 0));

                MethodReference readFunc = GetReadFunc(field.FieldType, recursionCount + 1);
                if (readFunc != null)
                {
                    worker.Append(worker.Create(OpCodes.Ldarg_0));
                    worker.Append(worker.Create(OpCodes.Call, readFunc));
                }
                else
                {
                    Weaver.Error("GetReadFunc for " + field.Name + " type " + field.FieldType + " no supported");
                    return null;
                }

                worker.Append(worker.Create(OpCodes.Stfld, field));
                fields++;
            }
            if (fields == 0)
            {
                Log.Warning("The class / struct " + variable.Name + " has no public or non-static fields to serialize");
            }

            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

    }

}