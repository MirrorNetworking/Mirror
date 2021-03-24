using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;
using Mono.CecilX.Rocks;

namespace Mirror.Weaver
{
    public static class Readers
    {
        const int MaxRecursionCount = 128;
        static Dictionary<string, MethodReference> readFuncs;

        public static void Init()
        {
            readFuncs = new Dictionary<string, MethodReference>();
        }

        internal static void Register(TypeReference dataType, MethodReference methodReference)
        {
            readFuncs[dataType.FullName] = methodReference;
        }

        public static MethodReference GetReadFunc(TypeReference variableReference, int recursionCount = 0)
        {
            if (readFuncs.TryGetValue(variableReference.FullName, out MethodReference foundFunc))
            {
                return foundFunc;
            }

            MethodDefinition newReaderFunc;

            // Arrays are special,  if we resolve them, we get teh element type,
            // so the following ifs might choke on it for scriptable objects
            // or other objects that require a custom serializer
            // thus check if it is an array and skip all the checks.
            if (variableReference.IsArray)
            {
                newReaderFunc = GenerateArrayReadFunc(variableReference, recursionCount);
                if (newReaderFunc != null)
                {
                    RegisterReadFunc(variableReference.FullName, newReaderFunc);
                }
                return newReaderFunc;
            }

            TypeDefinition variableDefinition = variableReference.Resolve();
            if (variableDefinition == null)
            {
                Weaver.Error($"{variableReference.Name} is not a supported type", variableReference);
                return null;
            }
            if (variableDefinition.IsDerivedFrom(WeaverTypes.ComponentType))
            {
                Weaver.Error($"Cannot generate reader for component type {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                return null;
            }
            if (variableReference.FullName == WeaverTypes.ObjectType.FullName)
            {
                Weaver.Error($"Cannot generate reader for {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                return null;
            }
            if (variableReference.FullName == WeaverTypes.ScriptableObjectType.FullName)
            {
                Weaver.Error($"Cannot generate reader for {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                return null;
            }
            if (variableReference.IsByReference)
            {
                // error??
                Weaver.Error($"Cannot pass type {variableReference.Name} by reference", variableReference);
                return null;
            }
            if (variableDefinition.HasGenericParameters && !variableDefinition.IsArraySegment() && !variableDefinition.IsList())
            {
                Weaver.Error($"Cannot generate reader for generic variable {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                return null;
            }
            if (variableDefinition.IsInterface)
            {
                Weaver.Error($"Cannot generate reader for interface {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                return null;
            }
            if (variableDefinition.IsAbstract)
            {
                Weaver.Error($"Cannot generate reader for abstract class {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                return null;
            }

            if (variableDefinition.IsEnum)
            {
                return GetReadFunc(variableDefinition.GetEnumUnderlyingType(), recursionCount);
            }
            else if (variableDefinition.IsArraySegment())
            {
                newReaderFunc = GenerateArraySegmentReadFunc(variableReference, recursionCount);
            }
            else if (variableDefinition.IsList())
            {
                newReaderFunc = GenerateListReadFunc(variableReference, recursionCount);
            }
            else
            {
                newReaderFunc = GenerateClassOrStructReadFunction(variableReference, recursionCount);
            }

            if (newReaderFunc == null)
            {
                Weaver.Error($"{variableReference.Name} is not a supported type", variableReference);
                return null;
            }
            RegisterReadFunc(variableReference.FullName, newReaderFunc);
            return newReaderFunc;
        }

        static void RegisterReadFunc(string name, MethodDefinition newReaderFunc)
        {
            readFuncs[name] = newReaderFunc;
            Weaver.WeaveLists.generatedReadFunctions.Add(newReaderFunc);

            Weaver.ConfirmGeneratedCodeClass();
            Weaver.WeaveLists.generateContainerClass.Methods.Add(newReaderFunc);
        }

        // TODO GenerateForLoop() helper again:
        // 7c2a6595eb9a246481370623a3ffafc665646c41

        static MethodDefinition GenerateArrayReadFunc(TypeReference variable, int recursionCount)
        {
            if (!variable.IsArrayType())
            {
                Weaver.Error($"{variable.Name} is an unsupported type. Jagged and multidimensional arrays are not supported", variable);
                return null;
            }

            TypeReference elementType = variable.GetElementType();
            MethodReference elementReadFunc = GetReadFunc(elementType, recursionCount + 1);
            if (elementReadFunc == null)
            {
                Weaver.Error($"Cannot generate reader for Array because element {elementType.Name} does not have a reader. Use a supported type or provide a custom reader", variable);
                return null;
            }

            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            // int length
            readerFunc.Body.Variables.Add(new VariableDefinition(WeaverTypes.int32Type));
            // T[] array
            readerFunc.Body.Variables.Add(new VariableDefinition(variable));
            // int it;
            readerFunc.Body.Variables.Add(new VariableDefinition(WeaverTypes.int32Type));

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            // int length = reader.ReadPackedInt32();
            GenerateReadLength(worker);

            // if (length < 0) {
            //    return null
            // }
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ldc_I4_0);
            Instruction labelEmptyArray = worker.Create(OpCodes.Nop);
            worker.Emit(OpCodes.Bge, labelEmptyArray);
            // return null
            worker.Emit(OpCodes.Ldnull);
            worker.Emit(OpCodes.Ret);
            worker.Append(labelEmptyArray);

            // T value = new T[length];
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Newarr, variable.GetElementType());
            worker.Emit(OpCodes.Stloc_1);

            // for (int i=0; i< length ; i++) {
            worker.Emit(OpCodes.Ldc_I4_0);
            worker.Emit(OpCodes.Stloc_2);
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Emit(OpCodes.Br, labelHead);

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);
            // value[i] = reader.ReadT();
            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ldloc_2);
            worker.Emit(OpCodes.Ldelema, variable.GetElementType());
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Call, elementReadFunc);
            worker.Emit(OpCodes.Stobj, variable.GetElementType());

            worker.Emit(OpCodes.Ldloc_2);
            worker.Emit(OpCodes.Ldc_I4_1);
            worker.Emit(OpCodes.Add);
            worker.Emit(OpCodes.Stloc_2);

            // loop while check
            worker.Append(labelHead);
            worker.Emit(OpCodes.Ldloc_2);
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Blt, labelBody);

            // return value;
            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ret);
            return readerFunc;
        }

        static void GenerateReadLength(ILProcessor worker)
        {
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, GetReadFunc(WeaverTypes.int32Type)));
            worker.Append(worker.Create(OpCodes.Stloc_0));
        }

        static MethodDefinition GenerateArraySegmentReadFunc(TypeReference variable, int recursionCount)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];

            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            // just deserialize the array and transform to array segment

            // $array = reader.Read<[T]>()
            ArrayType arrayType = elementType.MakeArrayType();
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, GetReadFunc(arrayType)));

            // return new ArraySegment<T>($array);
            worker.Emit(OpCodes.Newobj, WeaverTypes.ArraySegmentConstructorReference.MakeHostInstanceGeneric(genericInstance));
            worker.Emit(OpCodes.Ret);
            return readerFunc;
        }

        static MethodDefinition GenerateReaderFunction(TypeReference variable)
        {
            string functionName = "_Read_" + variable.FullName;

            // create new reader for this type
            MethodDefinition readerFunc = new MethodDefinition(
                functionName,
                MethodAttributes.Public |
                MethodAttributes.Static |
                MethodAttributes.HideBySig,
                Weaver.CurrentAssembly.MainModule.ImportReference(variable)
            );

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(WeaverTypes.NetworkReaderType)));
            readerFunc.Body.InitLocals = true;
            return readerFunc;
        }

        static MethodDefinition GenerateListReadFunc(TypeReference variable, int recursionCount)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];

            MethodReference elementReadFunc = GetReadFunc(elementType, recursionCount + 1);
            if (elementReadFunc == null)
            {
                Weaver.Error($"Cannot generate reader for List because element {elementType.Name} does not have a reader. Use a supported type or provide a custom reader", variable);
                return null;
            }

            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            // int length
            readerFunc.Body.Variables.Add(new VariableDefinition(WeaverTypes.int32Type));
            // list
            readerFunc.Body.Variables.Add(new VariableDefinition(variable));
            // int i
            readerFunc.Body.Variables.Add(new VariableDefinition(WeaverTypes.int32Type));

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            // int length = reader.ReadPackedInt32();
            GenerateReadLength(worker);

            // -1 is null list, so if count is less than 0 return null
            // if (count < 0) {
            //    return null
            // }
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ldc_I4_0);
            Instruction labelEmptyArray = worker.Create(OpCodes.Nop);
            worker.Emit(OpCodes.Bge, labelEmptyArray);
            // return null
            worker.Emit(OpCodes.Ldnull);
            worker.Emit(OpCodes.Ret);
            worker.Append(labelEmptyArray);

            // List<T> list = new List<T>();
            worker.Emit(OpCodes.Newobj, WeaverTypes.ListConstructorReference.MakeHostInstanceGeneric(genericInstance));
            worker.Emit(OpCodes.Stloc_1);

            // loop through array and deserialize each element
            // generates code like this
            // for (int i=0; i< length ; i++)
            // {
            //     list[i] = reader.ReadXXX();
            // }
            worker.Emit(OpCodes.Ldc_I4_0);
            worker.Emit(OpCodes.Stloc_2);
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Emit(OpCodes.Br, labelHead);

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);

            MethodReference addItem = WeaverTypes.ListAddReference.MakeHostInstanceGeneric(genericInstance);

            // list.Add(reader.ReadT());
            worker.Emit(OpCodes.Ldloc_1); // list
            worker.Emit(OpCodes.Ldarg_0); // reader
            worker.Emit(OpCodes.Call, elementReadFunc); // Read
            worker.Emit(OpCodes.Call, addItem); // set_Item

            // end for loop

            // for loop i++
            worker.Emit(OpCodes.Ldloc_2);
            worker.Emit(OpCodes.Ldc_I4_1);
            worker.Emit(OpCodes.Add);
            worker.Emit(OpCodes.Stloc_2);

            // loop while check
            worker.Append(labelHead);
            // for loop i < count
            worker.Emit(OpCodes.Ldloc_2);
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Blt, labelBody);

            // return value;
            worker.Emit(OpCodes.Ldloc_1);
            worker.Emit(OpCodes.Ret);
            return readerFunc;
        }


        static MethodDefinition GenerateClassOrStructReadFunction(TypeReference variable, int recursionCount)
        {
            if (recursionCount > MaxRecursionCount)
            {
                Weaver.Error($"{variable.Name} can't be deserialized because it references itself", variable);
                return null;
            }

            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            // create local for return value
            readerFunc.Body.Variables.Add(new VariableDefinition(variable));

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            TypeDefinition td = variable.Resolve();

            if (!td.IsValueType)
                GenerateNullCheck(worker);

            CreateNew(variable, worker, td);
            ReadAllFields(variable, recursionCount, worker);

            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ret);
            return readerFunc;
        }

        private static void GenerateNullCheck(ILProcessor worker)
        {
            // if (!reader.ReadBoolean()) {
            //   return null;
            // }

            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, GetReadFunc(WeaverTypes.boolType)));

            Instruction labelEmptyArray = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Brtrue, labelEmptyArray));
            // return null
            worker.Append(worker.Create(OpCodes.Ldnull));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(labelEmptyArray);
        }

        // Initialize the local variable with a new instance
        static void CreateNew(TypeReference variable, ILProcessor worker, TypeDefinition td)
        {
            if (variable.IsValueType)
            {
                // structs are created with Initobj
                worker.Emit(OpCodes.Ldloca, 0);
                worker.Emit(OpCodes.Initobj, variable);
            }
            else if (td.IsDerivedFrom(WeaverTypes.ScriptableObjectType))
            {
                GenericInstanceMethod genericInstanceMethod = new GenericInstanceMethod(WeaverTypes.ScriptableObjectCreateInstanceMethod);
                genericInstanceMethod.GenericArguments.Add(variable);
                worker.Emit(OpCodes.Call, genericInstanceMethod);
                worker.Emit(OpCodes.Stloc_0);
            }
            else
            {
                // classes are created with their constructor

                MethodDefinition ctor = Resolvers.ResolveDefaultPublicCtor(variable);
                if (ctor == null)
                {
                    Weaver.Error($"{variable.Name} can't be deserialized because it has no default constructor", variable);
                    return;
                }

                MethodReference ctorRef = Weaver.CurrentAssembly.MainModule.ImportReference(ctor);

                worker.Emit(OpCodes.Newobj, ctorRef);
                worker.Emit(OpCodes.Stloc_0);
            }
        }

        static void ReadAllFields(TypeReference variable, int recursionCount, ILProcessor worker)
        {
            uint fields = 0;
            foreach (FieldDefinition field in variable.FindAllPublicFields())
            {
                // mismatched ldloca/ldloc for struct/class combinations is invalid IL, which causes crash at runtime
                OpCode opcode = variable.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc;
                worker.Emit(opcode, 0);

                MethodReference readFunc = GetReadFunc(field.FieldType, recursionCount + 1);
                if (readFunc != null)
                {
                    worker.Emit(OpCodes.Ldarg_0);
                    worker.Emit(OpCodes.Call, readFunc);
                }
                else
                {
                    Weaver.Error($"{field.Name} has an unsupported type", field);
                }
                FieldReference fieldRef = Weaver.CurrentAssembly.MainModule.ImportReference(field);

                worker.Emit(OpCodes.Stfld, fieldRef);
                fields++;
            }

            if (fields == 0)
            {
                Log.Warning($"{variable} has no public or non-static fields to deserialize");
            }
        }

    }

}
