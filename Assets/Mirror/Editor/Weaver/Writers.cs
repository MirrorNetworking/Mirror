using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{

    public static class Writers
    {
        const int MaxRecursionCount = 128;

        static Dictionary<string, MethodReference> writeFuncs;

        public static void Init()
        {
            writeFuncs = new Dictionary<string, MethodReference>();
        }

        public static void Register(TypeReference dataType, MethodReference methodReference)
        {
            writeFuncs[dataType.FullName] = methodReference;
        }

        static void RegisterWriteFunc(string name, MethodDefinition newWriterFunc)
        {
            writeFuncs[name] = newWriterFunc;
            Weaver.WeaveLists.generatedWriteFunctions.Add(newWriterFunc);

            Weaver.ConfirmGeneratedCodeClass();
            Weaver.WeaveLists.generateContainerClass.Methods.Add(newWriterFunc);
        }

        public static MethodReference GetWriteFunc(TypeReference variable, int recursionCount = 0)
        {
            if (writeFuncs.TryGetValue(variable.FullName, out MethodReference foundFunc))
            {
                return foundFunc;
            }
            else if (variable.Resolve().IsEnum)
            {
                // serialize enum as their base type
                return GetWriteFunc(variable.Resolve().GetEnumUnderlyingType());
            }
            else
            {
                MethodDefinition newWriterFunc = GenerateWriter(variable, recursionCount);
                if (newWriterFunc != null)
                {
                    RegisterWriteFunc(variable.FullName, newWriterFunc);
                }
                return newWriterFunc;
            }
        }

        static MethodDefinition GenerateWriter(TypeReference variableReference, int recursionCount = 0)
        {
            // TODO: do we need this check? do we ever receieve types that are "ByReference"s
            if (variableReference.IsByReference)
            {
                // error??
                Weaver.Error($"Cannot pass {variableReference.Name} by reference", variableReference);
                return null;
            }

            // Arrays are special, if we resolve them, we get the element type,
            // eg int[] resolves to int
            // therefore process this before checks below
            if (variableReference.IsArray)
            {
                return GenerateArrayWriteFunc(variableReference, recursionCount);
            }

            // check for collections

            if (variableReference.IsArraySegment())
            {
                return GenerateArraySegmentWriteFunc(variableReference, recursionCount);
            }
            if (variableReference.IsList())
            {
                return GenerateListWriteFunc(variableReference, recursionCount);
            }

            // check for invalid types

            TypeDefinition variableDefinition = variableReference.Resolve();
            if (variableDefinition == null)
            {
                Weaver.Error($"{variableReference.Name} is not a supported type. Use a supported type or provide a custom writer", variableReference);
                return null;
            }
            if (variableDefinition.IsDerivedFrom(WeaverTypes.ComponentType))
            {
                Weaver.Error($"Cannot generate writer for component type {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
                return null;
            }
            if (variableReference.FullName == WeaverTypes.ObjectType.FullName)
            {
                Weaver.Error($"Cannot generate writer for {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
                return null;
            }
            if (variableReference.FullName == WeaverTypes.ScriptableObjectType.FullName)
            {
                Weaver.Error($"Cannot generate writer for {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
                return null;
            }
            if (variableDefinition.HasGenericParameters)
            {
                Weaver.Error($"Cannot generate writer for generic type {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
                return null;
            }
            if (variableDefinition.IsInterface)
            {
                Weaver.Error($"Cannot generate writer for interface {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
                return null;
            }
            if (variableDefinition.IsAbstract)
            {
                Weaver.Error($"Cannot generate writer for abstract class {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
                return null;
            }

            // generate writer for class/struct

            return GenerateClassOrStructWriterFunction(variableReference, recursionCount);
        }

        static MethodDefinition GenerateWriterFunc(TypeReference variable)
        {
            string functionName = "_Write_" + variable.FullName;

            // create new writer for this type
            MethodDefinition writerFunc = new MethodDefinition(functionName,
                MethodAttributes.Public |
                MethodAttributes.Static |
                MethodAttributes.HideBySig,
                WeaverTypes.voidType);

            writerFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(WeaverTypes.NetworkWriterType)));
            writerFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(variable)));
            writerFunc.Body.InitLocals = true;
            return writerFunc;
        }

        static MethodDefinition GenerateClassOrStructWriterFunction(TypeReference variable, int recursionCount)
        {
            if (recursionCount > MaxRecursionCount)
            {
                Weaver.Error($"{variable.Name} can't be serialized because it references itself", variable);
                return null;
            }

            MethodDefinition writerFunc = GenerateWriterFunc(variable);

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            if (!variable.Resolve().IsValueType)
                WriteNullCheck(worker);

            if (!WriteAllFields(variable, recursionCount, worker))
                return null;

            worker.Emit(OpCodes.Ret);
            return writerFunc;
        }

        private static void WriteNullCheck(ILProcessor worker)
        {
            // if (value == null)
            // {
            //     writer.WriteBoolean(false);
            //     return;
            // }

            Instruction labelNotNull = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Brtrue, labelNotNull));
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Call,  GetWriteFunc(WeaverTypes.boolType)));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(labelNotNull);

            // write.WriteBoolean(true);
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4_1));
            worker.Append(worker.Create(OpCodes.Call, GetWriteFunc(WeaverTypes.boolType)));
        }

        /// <summary>
        /// Find all fields in type and write them
        /// </summary>
        /// <param name="variable"></param>
        /// <param name="recursionCount"></param>
        /// <param name="worker"></param>
        /// <returns>false if fail</returns>
        static bool WriteAllFields(TypeReference variable, int recursionCount, ILProcessor worker)
        {
            uint fields = 0;
            foreach (FieldDefinition field in variable.FindAllPublicFields())
            {
                MethodReference writeFunc = GetWriteFunc(field.FieldType, recursionCount + 1);
                if (writeFunc != null)
                {
                    FieldReference fieldRef = Weaver.CurrentAssembly.MainModule.ImportReference(field);

                    fields++;
                    worker.Emit(OpCodes.Ldarg_0);
                    worker.Emit(OpCodes.Ldarg_1);
                    worker.Emit(OpCodes.Ldfld, fieldRef);
                    worker.Emit(OpCodes.Call, writeFunc);
                }
                else
                {
                    Weaver.Error($"{field.Name} has unsupported type. Use a type supported by Mirror instead", field);
                    return false;
                }
            }

            if (fields == 0)
            {
                Log.Warning($"{variable} has no no public or non-static fields to serialize");
            }

            return true;
        }

        // TODO GenerateForLoop() helper again from:
        // 5c4d8a27cc8234df14863bf7ea576a99d39d109c

        static MethodDefinition GenerateArrayWriteFunc(TypeReference variable, int recursionCount)
        {
            if (variable.IsMultidimensionalArray())
            {
                Weaver.Error($"{variable.Name} is an unsupported type. Jagged and multidimensional arrays are not supported", variable);
                return null;
            }

            TypeReference elementType = variable.GetElementType();
            MethodReference elementWriteFunc = GetWriteFunc(elementType, recursionCount + 1);
            MethodReference intWriterFunc = GetWriteFunc(WeaverTypes.int32Type);
            if (elementWriteFunc == null)
            {
                Weaver.Error($"Cannot generate writer for Array because element {elementType.Name} does not have a writer. Use a supported type or provide a custom writer", variable);
                return null;
            }

            MethodDefinition writerFunc = GenerateWriterFunc(variable);
            // int length
            writerFunc.Body.Variables.Add(new VariableDefinition(WeaverTypes.int32Type));
            // int i
            writerFunc.Body.Variables.Add(new VariableDefinition(WeaverTypes.int32Type));

            ILProcessor worker = writerFunc.Body.GetILProcessor();
            GenerateContainerNullCheck(worker);

            // int length = value.Length;
            worker.Emit(OpCodes.Ldarg_1);
            worker.Emit(OpCodes.Ldlen);
            worker.Emit(OpCodes.Stloc_0);

            // writer.WritePackedInt32(length);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Call, intWriterFunc);

            // for (int i=0; i< value.length; i++) {
            worker.Emit(OpCodes.Ldc_I4_0);
            worker.Emit(OpCodes.Stloc_1);
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Emit(OpCodes.Br, labelHead);

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);
            // writer.Write(value[i]);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldarg_1);
            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ldelema, elementType);
            worker.Emit(OpCodes.Ldobj, elementType);
            worker.Emit(OpCodes.Call, elementWriteFunc);

            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ldc_I4_1);
            worker.Emit(OpCodes.Add);
            worker.Emit(OpCodes.Stloc_1);

            // end for loop
            worker.Append(labelHead);
            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ldarg_1);
            worker.Emit(OpCodes.Ldlen);
            worker.Emit(OpCodes.Conv_I4);
            worker.Emit(OpCodes.Blt, labelBody);

            // return
            worker.Emit(OpCodes.Ret);
            return writerFunc;
        }

        static void GenerateContainerNullCheck(ILProcessor worker)
        {
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
            worker.Append(worker.Create(OpCodes.Call, GetWriteFunc(WeaverTypes.int32Type)));
            worker.Append(worker.Create(OpCodes.Ret));

            // else not null
            worker.Append(labelNull);
        }

        static MethodDefinition GenerateArraySegmentWriteFunc(TypeReference variable, int recursionCount)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];
            MethodReference elementWriteFunc = GetWriteFunc(elementType, recursionCount + 1);
            MethodReference intWriterFunc = GetWriteFunc(WeaverTypes.int32Type);

            if (elementWriteFunc == null)
            {
                Weaver.Error($"Cannot generate writer for ArraySegment because element {elementType.Name} does not have a writer. Use a supported type or provide a custom writer", variable);
                return null;
            }

            MethodDefinition writerFunc = GenerateWriterFunc(variable);
            // int length
            writerFunc.Body.Variables.Add(new VariableDefinition(WeaverTypes.int32Type));
            // int i
            writerFunc.Body.Variables.Add(new VariableDefinition(WeaverTypes.int32Type));

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            MethodReference countref = WeaverTypes.ArraySegmentCountReference.MakeHostInstanceGeneric(genericInstance);

            // int length = value.Count;
            worker.Emit(OpCodes.Ldarga_S, (byte)1);
            worker.Emit(OpCodes.Call, countref);
            worker.Emit(OpCodes.Stloc_0);


            // writer.WritePackedInt32(length);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Call, intWriterFunc);

            // Loop through the ArraySegment<T> and call the writer for each element.
            // generates this:
            // for (int i=0; i< length; i++)
            // {
            //    writer.Write(value.Array[i + value.Offset]);
            // }
            worker.Emit(OpCodes.Ldc_I4_0);
            worker.Emit(OpCodes.Stloc_1);
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Emit(OpCodes.Br, labelHead);

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);

            // writer.Write(value.Array[i + value.Offset]);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldarga_S, (byte)1);
            worker.Emit(OpCodes.Call, WeaverTypes.ArraySegmentArrayReference.MakeHostInstanceGeneric(genericInstance));
            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ldarga_S, (byte)1);
            worker.Emit(OpCodes.Call, WeaverTypes.ArraySegmentOffsetReference.MakeHostInstanceGeneric(genericInstance));
            worker.Emit(OpCodes.Add);
            worker.Emit(OpCodes.Ldelema, elementType);
            worker.Emit(OpCodes.Ldobj, elementType);
            worker.Emit(OpCodes.Call, elementWriteFunc);


            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ldc_I4_1);
            worker.Emit(OpCodes.Add);
            worker.Emit(OpCodes.Stloc_1);

            // end for loop
            worker.Append(labelHead);
            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Blt, labelBody);

            // return
            worker.Emit(OpCodes.Ret);
            return writerFunc;
        }

        static MethodDefinition GenerateListWriteFunc(TypeReference variable, int recursionCount)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];
            MethodReference elementWriteFunc = GetWriteFunc(elementType, recursionCount + 1);
            MethodReference intWriterFunc = GetWriteFunc(WeaverTypes.int32Type);

            if (elementWriteFunc == null)
            {
                Weaver.Error($"Cannot generate writer for List because element {elementType.Name} does not have a writer. Use a supported type or provide a custom writer", variable);
                return null;
            }

            MethodDefinition writerFunc = GenerateWriterFunc(variable);
            // int length
            writerFunc.Body.Variables.Add(new VariableDefinition(WeaverTypes.int32Type));
            // int i
            writerFunc.Body.Variables.Add(new VariableDefinition(WeaverTypes.int32Type));

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            GenerateContainerNullCheck(worker);

            MethodReference countref = WeaverTypes.ListCountReference.MakeHostInstanceGeneric(genericInstance);

            // int count = value.Count;
            worker.Emit(OpCodes.Ldarg_1);
            worker.Emit(OpCodes.Call, countref);
            worker.Emit(OpCodes.Stloc_0);

            // writer.WritePackedInt32(count);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Call, intWriterFunc);

            // Loop through the List<T> and call the writer for each element.
            // generates this:
            // for (int i=0; i < count; i++)
            // {
            //    writer.WriteT(value[i]);
            // }
            worker.Emit(OpCodes.Ldc_I4_0);
            worker.Emit(OpCodes.Stloc_1);
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Emit(OpCodes.Br, labelHead);

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);

            MethodReference getItem = WeaverTypes.ListGetItemReference.MakeHostInstanceGeneric(genericInstance);

            // writer.Write(value[i]);
            worker.Emit(OpCodes.Ldarg_0); // writer
            worker.Emit(OpCodes.Ldarg_1); // value
            worker.Emit(OpCodes.Ldloc_1); // i
            worker.Emit(OpCodes.Call, getItem); //get_Item
            worker.Emit(OpCodes.Call, elementWriteFunc); // Write


            // end for loop

            // for loop i++
            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ldc_I4_1);
            worker.Emit(OpCodes.Add);
            worker.Emit(OpCodes.Stloc_1);

            worker.Append(labelHead);
            // for loop i < count
            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Blt, labelBody);

            // return
            worker.Emit(OpCodes.Ret);
            return writerFunc;
        }
    }
}
