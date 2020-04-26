using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

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

        public static MethodReference GetReadFunc(TypeReference variable, int recursionCount = 0)
        {
            if (readFuncs.TryGetValue(variable.FullName, out MethodReference foundFunc))
            {
                return foundFunc;
            }

            MethodDefinition newReaderFunc;

            // Arrays are special,  if we resolve them, we get teh element type,
            // so the following ifs might choke on it for scriptable objects
            // or other objects that require a custom serializer
            // thus check if it is an array and skip all the checks.
            if (variable.IsArray)
            {
                newReaderFunc = GenerateArrayReadFunc(variable, recursionCount);
                RegisterReadFunc(variable.FullName, newReaderFunc);
                return newReaderFunc;
            }

            TypeDefinition td = variable.Resolve();
            if (td == null)
            {
                Weaver.Error($"{variable.Name} is not a supported type", variable);
                return null;
            }
            if (td.IsDerivedFrom(Weaver.ComponentType))
            {
                Weaver.Error($"Cannot generate reader for component type {variable.Name}. Use a supported type or provide a custom reader", variable);
                return null;
            }
            if (variable.FullName == Weaver.ObjectType.FullName)
            {
                Weaver.Error($"Cannot generate reader for {variable.Name}. Use a supported type or provide a custom reader", variable);
                return null;
            }
            if (variable.FullName == Weaver.ScriptableObjectType.FullName)
            {
                Weaver.Error($"Cannot generate reader for {variable.Name}. Use a supported type or provide a custom reader", variable);
                return null;
            }
            if (variable.IsByReference)
            {
                // error??
                Weaver.Error($"Cannot pass type {variable.Name} by reference", variable);
                return null;
            }
            if (td.HasGenericParameters && !td.FullName.StartsWith("System.ArraySegment`1", System.StringComparison.Ordinal))
            {
                Weaver.Error($"Cannot generate reader for generic variable {variable.Name}. Use a supported type or provide a custom reader", variable);
                return null;
            }
            if (td.IsInterface)
            {
                Weaver.Error($"Cannot generate reader for interface {variable.Name}. Use a supported type or provide a custom reader", variable);
                return null;
            }

            if (td.IsEnum)
            {
                return GetReadFunc(td.GetEnumUnderlyingType(), recursionCount);
            }
            else if (variable.FullName.StartsWith("System.ArraySegment`1", System.StringComparison.Ordinal))
            {
                newReaderFunc = GenerateArraySegmentReadFunc(variable, recursionCount);
            }
            else
            {
                newReaderFunc = GenerateClassOrStructReadFunction(variable, recursionCount);
            }

            if (newReaderFunc == null)
            {
                Weaver.Error($"{variable.Name} is not a supported type", variable);
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

        static MethodDefinition GenerateArrayReadFunc( TypeReference variable, int recursionCount)
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
            var readerFunc = new MethodDefinition(functionName,
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

            // int length = reader.ReadPackedInt32();
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, GetReadFunc(Weaver.int32Type)));
            worker.Append(worker.Create(OpCodes.Stloc_0));

            // if (length < 0) {
            //    return null
            // }
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            Instruction labelEmptyArray = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Bge, labelEmptyArray));
            // return null
            worker.Append(worker.Create(OpCodes.Ldnull));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(labelEmptyArray);

            // T value = new T[length];
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Newarr, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Stloc_1));

            // for (int i=0; i< length ; i++) {
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Stloc_2));
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Br, labelHead));

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);
            // value[i] = reader.ReadT();
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

            // return value;
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        static MethodDefinition GenerateArraySegmentReadFunc(TypeReference variable, int recursionCount)
        {
            var genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];

            MethodReference elementReadFunc = GetReadFunc(elementType, recursionCount + 1);
            if (elementReadFunc == null)
            {
                return null;
            }

            string functionName = "_ReadArraySegment_" + variable.GetElementType().Name + "_";
            if (variable.DeclaringType != null)
            {
                functionName += variable.DeclaringType.Name;
            }
            else
            {
                functionName += "None";
            }

            // create new reader for this type
            var readerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    variable);

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));

            // int lengh
            readerFunc.Body.Variables.Add(new VariableDefinition(Weaver.int32Type));
            // T[] array
            readerFunc.Body.Variables.Add(new VariableDefinition(elementType.MakeArrayType()));
            // int i;
            readerFunc.Body.Variables.Add(new VariableDefinition(Weaver.int32Type));
            readerFunc.Body.InitLocals = true;

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            // int length = reader.ReadPackedInt32();
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, GetReadFunc(Weaver.int32Type)));
            worker.Append(worker.Create(OpCodes.Stloc_0));

            // T[] array = new int[length]
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Newarr, elementType));
            worker.Append(worker.Create(OpCodes.Stloc_1));

            // loop through array and deserialize each element
            // generates code like this
            // for (int i=0; i< length ; i++)
            // {
            //     value[i] = reader.ReadXXX();
            // }
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Stloc_2));
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Br, labelHead));

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);
            // value[i] = reader.ReadT();
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldloc_2));
            worker.Append(worker.Create(OpCodes.Ldelema, elementType));
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, elementReadFunc));
            worker.Append(worker.Create(OpCodes.Stobj, elementType));

            worker.Append(worker.Create(OpCodes.Ldloc_2));
            worker.Append(worker.Create(OpCodes.Ldc_I4_1));
            worker.Append(worker.Create(OpCodes.Add));
            worker.Append(worker.Create(OpCodes.Stloc_2));

            // loop while check
            worker.Append(labelHead);
            worker.Append(worker.Create(OpCodes.Ldloc_2));
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Blt, labelBody));

            // return new ArraySegment<T>(array);
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Newobj, Weaver.ArraySegmentConstructorReference.MakeHostInstanceGeneric(genericInstance)));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        static MethodDefinition GenerateClassOrStructReadFunction(TypeReference variable, int recursionCount)
        {
            if (recursionCount > MaxRecursionCount)
            {
                Weaver.Error($"{variable.Name} can't be deserialized because it references itself", variable);
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
            var readerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    variable);

            // create local for return value
            readerFunc.Body.Variables.Add(new VariableDefinition(variable));
            readerFunc.Body.InitLocals = true;

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            TypeDefinition td = variable.Resolve();

            CreateNew(variable, worker, td);
            DeserializeFields(variable, recursionCount, worker);

            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        // Initialize the local variable with a new instance
        private static void CreateNew(TypeReference variable, ILProcessor worker, TypeDefinition td)
        {
            if (variable.IsValueType)
            {
                // structs are created with Initobj
                worker.Append(worker.Create(OpCodes.Ldloca, 0));
                worker.Append(worker.Create(OpCodes.Initobj, variable));
            }
            else if (td.IsDerivedFrom(Weaver.ScriptableObjectType))
            {
                var genericInstanceMethod = new GenericInstanceMethod(Weaver.ScriptableObjectCreateInstanceMethod);
                genericInstanceMethod.GenericArguments.Add(variable);
                worker.Append(worker.Create(OpCodes.Call, genericInstanceMethod));
                worker.Append(worker.Create(OpCodes.Stloc_0));
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

                worker.Append(worker.Create(OpCodes.Newobj, ctorRef));
                worker.Append(worker.Create(OpCodes.Stloc_0));
            }
        }

        private static void DeserializeFields(TypeReference variable, int recursionCount, ILProcessor worker)
        {
            uint fields = 0;
            foreach (FieldDefinition field in variable.Resolve().Fields)
            {
                if (field.IsStatic || field.IsPrivate)
                    continue;

                if (field.IsNotSerialized)
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
                    Weaver.Error($"{field.Name} has an unsupported type", field);
                }
                FieldReference fieldRef = Weaver.CurrentAssembly.MainModule.ImportReference(field);

                worker.Append(worker.Create(OpCodes.Stfld, fieldRef));
                fields++;
            }
            if (fields == 0)
            {
                Log.Warning($"{variable} has no public or non-static fields to deserialize");
            }
        }

    }

}
