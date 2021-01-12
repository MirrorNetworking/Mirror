using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using UnityEngine;

namespace Mirror.Weaver
{
    public class Readers
    {
        readonly Dictionary<TypeReference, MethodReference> readFuncs = new Dictionary<TypeReference, MethodReference>(new TypeReferenceComparer());

        private readonly ModuleDefinition module;
        private readonly IWeaverLogger logger;

        public int Count => readFuncs.Count;

        public Readers(ModuleDefinition module, IWeaverLogger logger)
        {
            this.module = module;
            this.logger = logger;
        }

        internal void Register(TypeReference dataType, MethodReference methodReference)
        {
            readFuncs[dataType] = methodReference;
        }

        public MethodReference GetReadFunc<T>(SequencePoint sequencePoint) =>
            GetReadFunc(module.ImportReference<T>(), sequencePoint);

        public MethodReference GetReadFunc(TypeReference typeReference, SequencePoint sequencePoint)
        {
            if (readFuncs.TryGetValue(typeReference, out MethodReference foundFunc))
            {
                return foundFunc;
            }

            typeReference = module.ImportReference(typeReference);
            if (typeReference.IsMultidimensionalArray())
            {
                logger.Error($"{typeReference.Name} is an unsupported type. Multidimensional arrays are not supported", typeReference, sequencePoint);
                return null;
            }

            if (typeReference.IsArray)
            {
                return GenerateReadCollection(typeReference, typeReference.GetElementType(), () => NetworkReaderExtensions.ReadArray<object>(default), sequencePoint);
            }

            TypeDefinition variableDefinition = typeReference.Resolve();

            if (variableDefinition == null)
            {
                logger.Error($"{typeReference.Name} is not a supported type", typeReference, sequencePoint);
                return null;
            }
            if (variableDefinition.Is(typeof(ArraySegment<>)))
            {
                return GenerateArraySegmentReadFunc(typeReference, sequencePoint);
            }
            if (variableDefinition.Is(typeof(List<>)))
            {
                var genericInstance = (GenericInstanceType)typeReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateReadCollection(typeReference, elementType, () => NetworkReaderExtensions.ReadList<object>(default), sequencePoint);
            }
            if (variableDefinition.IsEnum)
            {
                return GenerateEnumReadFunc(typeReference, sequencePoint);
            }
            if (variableDefinition.IsDerivedFrom<NetworkBehaviour>())
            {
                return GetNetworkBehaviourReader(typeReference);
            }
            if (variableDefinition.IsDerivedFrom<Component>())
            {
                logger.Error($"Cannot generate reader for component type {typeReference.Name}. Use a supported type or provide a custom reader", typeReference, sequencePoint);
                return null;
            }
            if (typeReference.Is<UnityEngine.Object>())
            {
                logger.Error($"Cannot generate reader for {typeReference.Name}. Use a supported type or provide a custom reader", typeReference, sequencePoint);
                return null;
            }
            if (typeReference.Is<ScriptableObject>())
            {
                logger.Error($"Cannot generate reader for {typeReference.Name}. Use a supported type or provide a custom reader", typeReference, sequencePoint);
                return null;
            }
            if (typeReference.IsByReference)
            {
                // error??
                logger.Error($"Cannot pass type {typeReference.Name} by reference", typeReference, sequencePoint);
                return null;
            }
            if (variableDefinition.HasGenericParameters)
            {
                logger.Error($"Cannot generate reader for generic variable {typeReference.Name}. Use a supported type or provide a custom reader", typeReference, sequencePoint);
                return null;
            }
            if (variableDefinition.IsInterface)
            {
                logger.Error($"Cannot generate reader for interface {typeReference.Name}. Use a supported type or provide a custom reader", typeReference, sequencePoint);
                return null;
            }
            if (variableDefinition.IsAbstract)
            {
                logger.Error($"Cannot generate reader for abstract class {typeReference.Name}. Use a supported type or provide a custom reader", typeReference, sequencePoint);
                return null;
            }

            return GenerateClassOrStructReadFunction(typeReference, sequencePoint);
        }

        private MethodReference GetNetworkBehaviourReader(TypeReference typeReference)
        {
            MethodReference readFunc = module.ImportReference<NetworkReader>((reader) => reader.ReadNetworkBehaviour());
            Register(typeReference, readFunc);
            return readFunc;
        }

        void RegisterReadFunc(TypeReference typeReference, MethodDefinition newReaderFunc)
        {
            readFuncs[typeReference] = newReaderFunc;
        }

        MethodDefinition GenerateEnumReadFunc(TypeReference variable, SequencePoint sequencePoint)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            worker.Append(worker.Create(OpCodes.Ldarg_0));

            TypeReference underlyingType = variable.Resolve().GetEnumUnderlyingType();
            MethodReference underlyingFunc = GetReadFunc(underlyingType, sequencePoint);

            worker.Append(worker.Create(OpCodes.Call, underlyingFunc));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        MethodDefinition GenerateArraySegmentReadFunc(TypeReference variable, SequencePoint sequencePoint)
        {
            var genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];

            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            // $array = reader.Read<[T]>()
            ArrayType arrayType = elementType.MakeArrayType();
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, GetReadFunc(arrayType, sequencePoint)));

            // return new ArraySegment<T>($array);
            MethodReference arraySegmentConstructor = module.ImportReference(() => new ArraySegment<object>());
            worker.Append(worker.Create(OpCodes.Newobj, arraySegmentConstructor.MakeHostInstanceGeneric(genericInstance)));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        private MethodDefinition GenerateReaderFunction(TypeReference variable)
        {
            string functionName = "_Read_" + variable.FullName;

            // create new reader for this type
            MethodDefinition readerFunc = module.GeneratedClass().AddMethod(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    variable);

            _ = readerFunc.AddParam<NetworkReader>("reader");
            readerFunc.Body.InitLocals = true;
            RegisterReadFunc(variable, readerFunc);

            return readerFunc;
        }

        MethodDefinition GenerateReadCollection(TypeReference variable, TypeReference elementType, Expression<Action> readerFunction, SequencePoint sequencePoint)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(variable);
            // generate readers for the element
            GetReadFunc(elementType, sequencePoint);

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

        MethodDefinition GenerateClassOrStructReadFunction(TypeReference type, SequencePoint sequencePoint)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(type);

            // create local for return value
            VariableDefinition variable = readerFunc.AddLocal(type);

            ILProcessor worker = readerFunc.Body.GetILProcessor();


            TypeDefinition td = type.Resolve();

            if (!td.IsValueType)
                GenerateNullCheck(worker, sequencePoint);

            CreateNew(variable, worker, td, sequencePoint);
            ReadAllFields(type, worker, sequencePoint);

            worker.Append(worker.Create(OpCodes.Ldloc, variable));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        private void GenerateNullCheck(ILProcessor worker, SequencePoint sequencePoint)
        {
            // if (!reader.ReadBoolean()) {
            //   return null;
            // }
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, GetReadFunc<bool>(sequencePoint)));

            Instruction labelEmptyArray = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Brtrue, labelEmptyArray));
            // return null
            worker.Append(worker.Create(OpCodes.Ldnull));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(labelEmptyArray);
        }

        // Initialize the local variable with a new instance
        void CreateNew(VariableDefinition variable, ILProcessor worker, TypeDefinition td, SequencePoint sequencePoint)
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
                    logger.Error($"{type.Name} can't be deserialized because it has no default constructor", type, sequencePoint);
                    return;
                }

                MethodReference ctorRef = worker.Body.Method.Module.ImportReference(ctor);

                worker.Append(worker.Create(OpCodes.Newobj, ctorRef));
                worker.Append(worker.Create(OpCodes.Stloc, variable));
            }
        }

        void ReadAllFields(TypeReference variable, ILProcessor worker, SequencePoint sequencePoint)
        {
            uint fields = 0;
            foreach (FieldDefinition field in variable.FindAllPublicFields())
            {
                // mismatched ldloca/ldloc for struct/class combinations is invalid IL, which causes crash at runtime
                OpCode opcode = variable.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc;
                worker.Append(worker.Create(opcode, 0));

                MethodReference readFunc = GetReadFunc(field.FieldType, sequencePoint);
                if (readFunc != null)
                {
                    worker.Append(worker.Create(OpCodes.Ldarg_0));
                    worker.Append(worker.Create(OpCodes.Call, readFunc));
                }
                else
                {
                    logger.Error($"{field.Name} has an unsupported type", field, sequencePoint);
                }
                FieldReference fieldRef = module.ImportReference(field);

                worker.Append(worker.Create(OpCodes.Stfld, fieldRef));
                fields++;
            }
        }

        /// <summary>
        /// Save a delegate for each one of the readers into <see cref="Reader{T}.Read"/>
        /// </summary>
        /// <param name="worker"></param>
        internal void InitializeReaders(ILProcessor worker)
        {
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
