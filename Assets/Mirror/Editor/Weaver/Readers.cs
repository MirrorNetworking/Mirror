using System;
using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;
using Mono.CecilX.Rocks;

namespace Mirror.Weaver
{
    public static class Readers
    {
        static Dictionary<TypeReference, MethodReference> readFuncs;

        public static void Init()
        {
            readFuncs = new Dictionary<TypeReference, MethodReference>(new TypeReferenceComparer());
        }

        internal static void Register(TypeReference dataType, MethodReference methodReference)
        {
            if (readFuncs.ContainsKey(dataType))
            {
                // TODO enable this again later.
                // Reader has some obsolete functions that were renamed.
                // Don't want weaver warnings for all of them.
                //Weaver.Warning($"Registering a Read method for {dataType.FullName} when one already exists", methodReference);
            }

            // we need to import type when we Initialize Readers so import here in case it is used anywhere else
            TypeReference imported = Weaver.CurrentAssembly.MainModule.ImportReference(dataType);
            readFuncs[imported] = methodReference;
        }

        static void RegisterReadFunc(TypeReference typeReference, MethodDefinition newReaderFunc)
        {
            Register(typeReference, newReaderFunc);

            Weaver.GeneratedCodeClass.Methods.Add(newReaderFunc);
        }

        /// <summary>
        /// Finds existing reader for type, if non exists trys to create one
        /// <para>This method is recursive</para>
        /// </summary>
        /// <param name="variable"></param>
        /// <returns>Returns <see cref="MethodReference"/> or null</returns>
        public static MethodReference GetReadFunc(TypeReference variable)
        {
            if (readFuncs.TryGetValue(variable, out MethodReference foundFunc))
            {
                return foundFunc;
            }
            else
            {
                TypeReference importedVariable = Weaver.CurrentAssembly.MainModule.ImportReference(variable);
                return GenerateReader(importedVariable);
            }
        }

        static MethodReference GenerateReader(TypeReference variableReference)
        {
            // Arrays are special,  if we resolve them, we get the element type,
            // so the following ifs might choke on it for scriptable objects
            // or other objects that require a custom serializer
            // thus check if it is an array and skip all the checks.
            if (variableReference.IsArray)
            {
                if (variableReference.IsMultidimensionalArray())
                {
                    Weaver.Error($"{variableReference.Name} is an unsupported type. Multidimensional arrays are not supported", variableReference);
                    return null;
                }

                return GenerateReadCollection(variableReference, variableReference.GetElementType(), nameof(NetworkReaderExtensions.ReadArray));
            }

            TypeDefinition variableDefinition = variableReference.Resolve();

            // check if the type is completely invalid
            if (variableDefinition == null)
            {
                Weaver.Error($"{variableReference.Name} is not a supported type", variableReference);
                return null;
            }
            else if (variableReference.IsByReference)
            {
                // error??
                Weaver.Error($"Cannot pass type {variableReference.Name} by reference", variableReference);
                return null;
            }

            // use existing func for known types
            if (variableDefinition.IsEnum)
            {
                return GenerateEnumReadFunc(variableReference);
            }
            else if (variableDefinition.Is(typeof(ArraySegment<>)))
            {
                return GenerateArraySegmentReadFunc(variableReference);
            }
            else if (variableDefinition.Is(typeof(List<>)))
            {
                GenericInstanceType genericInstance = (GenericInstanceType)variableReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateReadCollection(variableReference, elementType, nameof(NetworkReaderExtensions.ReadList));
            }
            else if (variableReference.IsDerivedFrom<NetworkBehaviour>())
            {
                return GetNetworkBehaviourReader(variableReference);
            }

            // check if reader generation is applicable on this type
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

        static MethodReference GetNetworkBehaviourReader(TypeReference variableReference)
        {
            // uses generic ReadNetworkBehaviour rather than having weaver create one for each NB
            MethodReference generic = WeaverTypes.readNetworkBehaviourGeneric;

            MethodReference readFunc = generic.MakeGeneric(variableReference);

            // register function so it is added to Reader<T>
            // use Register instead of RegisterWriteFunc because this is not a generated function
            Register(variableReference, readFunc);

            return readFunc;
        }

        static MethodDefinition GenerateEnumReadFunc(TypeReference variable)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            worker.Emit(OpCodes.Ldarg_0);

            TypeReference underlyingType = variable.Resolve().GetEnumUnderlyingType();
            MethodReference underlyingFunc = GetReadFunc(underlyingType);

            worker.Emit(OpCodes.Call, underlyingFunc);
            worker.Emit(OpCodes.Ret);
            return readerFunc;
        }

        static MethodDefinition GenerateArraySegmentReadFunc(TypeReference variable)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];

            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            // $array = reader.Read<[T]>()
            ArrayType arrayType = elementType.MakeArrayType();
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Call, GetReadFunc(arrayType));

            // return new ArraySegment<T>($array);
            worker.Emit(OpCodes.Newobj, WeaverTypes.ArraySegmentConstructorReference.MakeHostInstanceGeneric(genericInstance));
            worker.Emit(OpCodes.Ret);
            return readerFunc;
        }

        static MethodDefinition GenerateReaderFunction(TypeReference variable)
        {
            string functionName = "_Read_" + variable.FullName;

            // create new reader for this type
            MethodDefinition readerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    variable);

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, WeaverTypes.Import<NetworkReader>()));
            readerFunc.Body.InitLocals = true;
            RegisterReadFunc(variable, readerFunc);

            return readerFunc;
        }

        static MethodDefinition GenerateReadCollection(TypeReference variable, TypeReference elementType, string readerFunction)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(variable);
            // generate readers for the element
            GetReadFunc(elementType);

            ModuleDefinition module = Weaver.CurrentAssembly.MainModule;
            TypeReference readerExtensions = module.ImportReference(typeof(NetworkReaderExtensions));
            MethodReference listReader = Resolvers.ResolveMethod(readerExtensions, Weaver.CurrentAssembly, readerFunction);

            GenericInstanceMethod methodRef = new GenericInstanceMethod(listReader);
            methodRef.GenericArguments.Add(elementType);

            // generates
            // return reader.ReadList<T>();

            ILProcessor worker = readerFunc.Body.GetILProcessor();
            worker.Emit(OpCodes.Ldarg_0); // reader
            worker.Emit(OpCodes.Call, methodRef); // Read

            worker.Emit(OpCodes.Ret);

            return readerFunc;
        }

        static MethodDefinition GenerateClassOrStructReadFunction(TypeReference variable)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            // create local for return value
            readerFunc.Body.Variables.Add(new VariableDefinition(variable));

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            TypeDefinition td = variable.Resolve();

            if (!td.IsValueType)
                GenerateNullCheck(worker);

            CreateNew(variable, worker, td);
            ReadAllFields(variable, worker);

            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ret);
            return readerFunc;
        }

        static void GenerateNullCheck(ILProcessor worker)
        {
            // if (!reader.ReadBoolean()) {
            //   return null;
            // }
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Call, GetReadFunc(WeaverTypes.Import<bool>()));

            Instruction labelEmptyArray = worker.Create(OpCodes.Nop);
            worker.Emit(OpCodes.Brtrue, labelEmptyArray);
            // return null
            worker.Emit(OpCodes.Ldnull);
            worker.Emit(OpCodes.Ret);
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
            else if (td.IsDerivedFrom<UnityEngine.ScriptableObject>())
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

        static void ReadAllFields(TypeReference variable, ILProcessor worker)
        {
            foreach (FieldDefinition field in variable.FindAllPublicFields())
            {
                // mismatched ldloca/ldloc for struct/class combinations is invalid IL, which causes crash at runtime
                OpCode opcode = variable.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc;
                worker.Emit(opcode, 0);
                MethodReference readFunc = GetReadFunc(field.FieldType);
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
            }
        }

        /// <summary>
        /// Save a delegate for each one of the readers into <see cref="Reader{T}.read"/>
        /// </summary>
        /// <param name="worker"></param>
        internal static void InitializeReaders(ILProcessor worker)
        {
            ModuleDefinition module = Weaver.CurrentAssembly.MainModule;

            TypeReference genericReaderClassRef = module.ImportReference(typeof(Reader<>));

            System.Reflection.FieldInfo fieldInfo = typeof(Reader<>).GetField(nameof(Reader<object>.read));
            FieldReference fieldRef = module.ImportReference(fieldInfo);
            TypeReference networkReaderRef = module.ImportReference(typeof(NetworkReader));
            TypeReference funcRef = module.ImportReference(typeof(Func<,>));
            MethodReference funcConstructorRef = module.ImportReference(typeof(Func<,>).GetConstructors()[0]);

            foreach (KeyValuePair<TypeReference, MethodReference> kvp in readFuncs)
            {
                TypeReference targetType = kvp.Key;
                MethodReference readFunc = kvp.Value;

                // create a Func<NetworkReader, T> delegate
                worker.Emit(OpCodes.Ldnull);
                worker.Emit(OpCodes.Ldftn, readFunc);
                GenericInstanceType funcGenericInstance = funcRef.MakeGenericInstanceType(networkReaderRef, targetType);
                MethodReference funcConstructorInstance = funcConstructorRef.MakeHostInstanceGeneric(funcGenericInstance);
                worker.Emit(OpCodes.Newobj, funcConstructorInstance);

                // save it in Reader<T>.read
                GenericInstanceType genericInstance = genericReaderClassRef.MakeGenericInstanceType(targetType);
                FieldReference specializedField = fieldRef.SpecializeField(genericInstance);
                worker.Emit(OpCodes.Stsfld, specializedField);
            }

        }
    }
}
