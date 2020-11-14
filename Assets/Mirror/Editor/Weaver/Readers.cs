using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using UnityEngine;

namespace Mirror.Weaver
{
    public static class Readers
    {
        static Dictionary<string, MethodReference> readFuncs;

        public static int Count => readFuncs.Count;

        public static void Init()
        {
            readFuncs = new Dictionary<string, MethodReference>();
        }

        internal static void Register(TypeReference dataType, MethodReference methodReference)
        {
            readFuncs[dataType.FullName] = methodReference;
        }

        public static MethodReference GetReadFunc(TypeReference variableReference)
        {
            if (readFuncs.TryGetValue(variableReference.FullName, out MethodReference foundFunc))
            {
                return foundFunc;
            }

            if (variableReference.IsMultidimensionalArray())
            {
                Weaver.Error($"{variableReference.Name} is an unsupported type. Multidimensional arrays are not supported", variableReference);
                return null;
            }

            if (variableReference.IsArray)
            {
                return GenerateReadCollection(variableReference, variableReference.GetElementType(), () => NetworkReaderExtensions.ReadArray<object>(default));
            }

            TypeDefinition variableDefinition = variableReference.Resolve();

            if (variableDefinition == null)
            {
                Weaver.Error($"{variableReference.Name} is not a supported type", variableReference);
                return null;
            }
            if (variableDefinition.Is(typeof(ArraySegment<>)))
            {
                return GenerateArraySegmentReadFunc(variableReference);
            }
            if (variableDefinition.Is(typeof(List<>)))
            {
                var genericInstance = (GenericInstanceType)variableReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateReadCollection(variableReference, elementType, () => NetworkReaderExtensions.ReadList<object>(default));
            }
            if (variableDefinition.IsEnum)
            {
                return GenerateEnumReadFunc(variableReference);
            }
            if (variableDefinition.IsDerivedFrom<UnityEngine.Component>())
            {
                Weaver.Error($"Cannot generate reader for component type {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                return null;
            }
            if (variableReference.Is<UnityEngine.Object>())
            {
                Weaver.Error($"Cannot generate reader for {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                return null;
            }
            if (variableReference.Is<UnityEngine.ScriptableObject>())
            {
                Weaver.Error($"Cannot generate reader for {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                return null;
            }
            if (variableReference.Is<UnityEngine.GameObject>())
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
            if (variableDefinition.HasGenericParameters)
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

            return GenerateClassOrStructReadFunction(variableReference);
        }

        static void RegisterReadFunc(TypeReference typeReference, MethodDefinition newReaderFunc)
        {
            readFuncs[typeReference.FullName] = newReaderFunc;
        }

        static MethodDefinition GenerateEnumReadFunc(TypeReference variable)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            worker.Append(worker.Create(OpCodes.Ldarg_0));

            TypeReference underlyingType = variable.Resolve().GetEnumUnderlyingType();
            MethodReference underlyingFunc = GetReadFunc(underlyingType);

            worker.Append(worker.Create(OpCodes.Call, underlyingFunc));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        static MethodDefinition GenerateArraySegmentReadFunc(TypeReference variable)
        {
            var genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];

            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            // $array = reader.Read<[T]>()
            ArrayType arrayType = elementType.MakeArrayType();
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, GetReadFunc(arrayType)));

            // return new ArraySegment<T>($array);
            var arraySegmentConstructor = readerFunc.Module.ImportReference(() => new ArraySegment<object>());
            worker.Append(worker.Create(OpCodes.Newobj, arraySegmentConstructor.MakeHostInstanceGeneric(genericInstance)));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        private static MethodDefinition GenerateReaderFunction(TypeReference variable)
        {
            string functionName = "_Read_" + variable.FullName;

            // create new reader for this type
            MethodDefinition readerFunc = Weaver.CurrentAssembly.MainModule.GeneratedClass().AddMethod(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    Weaver.CurrentAssembly.MainModule.ImportReference(variable));

            _ = readerFunc.AddParam<NetworkReader>("reader");
            readerFunc.Body.InitLocals = true;
            RegisterReadFunc(variable, readerFunc);

            return readerFunc;
        }

        static MethodDefinition GenerateReadCollection(TypeReference variable, TypeReference elementType, Expression<Action> readerFunction)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(variable);
            // generate readers for the element
            GetReadFunc(elementType);

            ModuleDefinition module = Weaver.CurrentAssembly.MainModule;
            MethodReference listReader = module.ImportReference(readerFunction);

            var methodRef = new GenericInstanceMethod(listReader.GetElementMethod());
            methodRef.GenericArguments.Add(elementType);

            // generates
            // return reader.ReadList<T>();

            ILProcessor worker = readerFunc.Body.GetILProcessor();
            worker.Append(worker.Create(OpCodes.Ldarg_0)); // reader
            worker.Append(worker.Create(OpCodes.Call, methodRef)); // Read

            worker.Append(worker.Create(OpCodes.Ret));

            return readerFunc;
        }

        static MethodDefinition GenerateClassOrStructReadFunction(TypeReference type)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(type);

            // create local for return value
            VariableDefinition variable = readerFunc.AddLocal(type);

            ILProcessor worker = readerFunc.Body.GetILProcessor();


            TypeDefinition td = type.Resolve();

            if (!td.IsValueType)
                GenerateNullCheck(worker);

            CreateNew(variable, worker, td);
            ReadAllFields(type, worker);

            worker.Append(worker.Create(OpCodes.Ldloc, variable));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        private static void GenerateNullCheck(ILProcessor worker)
        {
            // if (!reader.ReadBoolean()) {
            //   return null;
            // }
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, GetReadFunc(WeaverTypes.Import<bool>())));

            Instruction labelEmptyArray = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Brtrue, labelEmptyArray));
            // return null
            worker.Append(worker.Create(OpCodes.Ldnull));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(labelEmptyArray);
        }

        // Initialize the local variable with a new instance
        static void CreateNew(VariableDefinition variable, ILProcessor worker, TypeDefinition td)
        {
            TypeReference type = variable.VariableType;
            if (type.IsValueType)
            {
                // structs are created with Initobj
                worker.Append(worker.Create(OpCodes.Ldloca, variable));
                worker.Append(worker.Create(OpCodes.Initobj, type));
            }
            else if (td.IsDerivedFrom<ScriptableObject>())
            {
                MethodReference createScriptableObjectInstance = worker.Body.Method.Module.ImportReference(() => ScriptableObject.CreateInstance<ScriptableObject>());
                var genericInstanceMethod = new GenericInstanceMethod(createScriptableObjectInstance.GetElementMethod());
                genericInstanceMethod.GenericArguments.Add(type);
                worker.Append(worker.Create(OpCodes.Call, genericInstanceMethod));
                worker.Append(worker.Create(OpCodes.Stloc, variable));
            }
            else
            {
                // classes are created with their constructor
                MethodDefinition ctor = Resolvers.ResolveDefaultPublicCtor(type);
                if (ctor == null)
                {
                    Weaver.Error($"{type.Name} can't be deserialized because it has no default constructor", type);
                    return;
                }

                MethodReference ctorRef = worker.Body.Method.Module.ImportReference(ctor);

                worker.Append(worker.Create(OpCodes.Newobj, ctorRef));
                worker.Append(worker.Create(OpCodes.Stloc, variable));
            }
        }

        static void ReadAllFields(TypeReference variable, ILProcessor worker)
        {
            uint fields = 0;
            foreach (FieldDefinition field in variable.FindAllPublicFields())
            {
                // mismatched ldloca/ldloc for struct/class combinations is invalid IL, which causes crash at runtime
                OpCode opcode = variable.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc;
                worker.Append(worker.Create(opcode, 0));

                TypeReference fieldTypeRef = worker.Body.Method.Module.ImportReference(field.FieldType);
                MethodReference readFunc = GetReadFunc(fieldTypeRef);
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
        }

        /// <summary>
        /// Save a delegate for each one of the readers into <see cref="Reader{T}.Read"/>
        /// </summary>
        /// <param name="worker"></param>
        internal static void InitializeReaders(ILProcessor worker)
        {
            ModuleDefinition module = Weaver.CurrentAssembly.MainModule;

            TypeReference genericReaderClassRef = module.ImportReference(typeof(Reader<>));

            System.Reflection.PropertyInfo readProperty = typeof(Reader<>).GetProperty(nameof(Reader<object>.Read));
            MethodReference fieldRef = module.ImportReference(readProperty.GetSetMethod());
            TypeReference networkReaderRef = module.ImportReference(typeof(NetworkReader));
            TypeReference funcRef = module.ImportReference(typeof(Func<,>));
            MethodReference funcConstructorRef = module.ImportReference(typeof(Func<,>).GetConstructors()[0]);

            foreach (MethodReference readFunc in readFuncs.Values)
            {
                TypeReference dataType = readFunc.ReturnType;

                // create a Func<NetworkReader, T> delegate
                worker.Append(worker.Create(OpCodes.Ldnull));
                worker.Append(worker.Create(OpCodes.Ldftn, readFunc));
                GenericInstanceType funcGenericInstance = funcRef.MakeGenericInstanceType(networkReaderRef, dataType);
                MethodReference funcConstructorInstance = funcConstructorRef.MakeHostInstanceGeneric(funcGenericInstance);
                worker.Append(worker.Create(OpCodes.Newobj, funcConstructorInstance));

                // save it in Reader<T>.Read
                GenericInstanceType genericInstance = genericReaderClassRef.MakeGenericInstanceType(dataType);
                MethodReference specializedField = fieldRef.MakeHostInstanceGeneric(genericInstance);
                worker.Append(worker.Create(OpCodes.Call, specializedField));
            }

        }
    }
}
