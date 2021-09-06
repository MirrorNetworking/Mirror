using System;
using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;
// to use Mono.CecilX.Rocks here, we need to 'override references' in the
// Unity.Mirror.CodeGen assembly definition file in the Editor, and add CecilX.Rocks.
// otherwise we get an unknown import exception.
using Mono.CecilX.Rocks;

namespace Mirror.Weaver
{
    // not static, because ILPostProcessor is multithreaded
    public class Readers
    {
        // Readers are only for this assembly.
        // can't be used from another assembly, otherwise we will get:
        // "System.ArgumentException: Member ... is declared in another module and needs to be imported"
        AssemblyDefinition assembly;
        WeaverTypes weaverTypes;
        TypeDefinition GeneratedCodeClass;
        Logger Log;

        Dictionary<TypeReference, MethodReference> readFuncs =
            new Dictionary<TypeReference, MethodReference>(new TypeReferenceComparer());

        public Readers(AssemblyDefinition assembly, WeaverTypes weaverTypes, TypeDefinition GeneratedCodeClass, Logger Log)
        {
            this.assembly = assembly;
            this.weaverTypes = weaverTypes;
            this.GeneratedCodeClass = GeneratedCodeClass;
            this.Log = Log;
        }

        internal void Register(TypeReference dataType, MethodReference methodReference)
        {
            if (readFuncs.ContainsKey(dataType))
            {
                // TODO enable this again later.
                // Reader has some obsolete functions that were renamed.
                // Don't want weaver warnings for all of them.
                //Weaver.Warning($"Registering a Read method for {dataType.FullName} when one already exists", methodReference);
            }

            // we need to import type when we Initialize Readers so import here in case it is used anywhere else
            TypeReference imported = assembly.MainModule.ImportReference(dataType);
            readFuncs[imported] = methodReference;
        }

        void RegisterReadFunc(TypeReference typeReference, MethodDefinition newReaderFunc)
        {
            Register(typeReference, newReaderFunc);
            GeneratedCodeClass.Methods.Add(newReaderFunc);
        }

        // Finds existing reader for type, if non exists trys to create one
        public MethodReference GetReadFunc(TypeReference variable, ref bool WeavingFailed)
        {
            if (readFuncs.TryGetValue(variable, out MethodReference foundFunc))
                return foundFunc;

            TypeReference importedVariable = assembly.MainModule.ImportReference(variable);
            return GenerateReader(importedVariable, ref WeavingFailed);
        }

        MethodReference GenerateReader(TypeReference variableReference, ref bool WeavingFailed)
        {
            // Arrays are special,  if we resolve them, we get the element type,
            // so the following ifs might choke on it for scriptable objects
            // or other objects that require a custom serializer
            // thus check if it is an array and skip all the checks.
            if (variableReference.IsArray)
            {
                if (variableReference.IsMultidimensionalArray())
                {
                    Log.Error($"{variableReference.Name} is an unsupported type. Multidimensional arrays are not supported", variableReference);
                    WeavingFailed = true;
                    return null;
                }

                return GenerateReadCollection(variableReference, variableReference.GetElementType(), nameof(NetworkReaderExtensions.ReadArray), ref WeavingFailed);
            }

            TypeDefinition variableDefinition = variableReference.Resolve();

            // check if the type is completely invalid
            if (variableDefinition == null)
            {
                Log.Error($"{variableReference.Name} is not a supported type", variableReference);
                WeavingFailed = true;
                return null;
            }
            else if (variableReference.IsByReference)
            {
                // error??
                Log.Error($"Cannot pass type {variableReference.Name} by reference", variableReference);
                WeavingFailed = true;
                return null;
            }

            // use existing func for known types
            if (variableDefinition.IsEnum)
            {
                return GenerateEnumReadFunc(variableReference, ref WeavingFailed);
            }
            else if (variableDefinition.Is(typeof(ArraySegment<>)))
            {
                return GenerateArraySegmentReadFunc(variableReference, ref WeavingFailed);
            }
            else if (variableDefinition.Is(typeof(List<>)))
            {
                GenericInstanceType genericInstance = (GenericInstanceType)variableReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateReadCollection(variableReference, elementType, nameof(NetworkReaderExtensions.ReadList), ref WeavingFailed);
            }
            else if (variableReference.IsDerivedFrom<NetworkBehaviour>())
            {
                return GetNetworkBehaviourReader(variableReference);
            }

            // check if reader generation is applicable on this type
            if (variableDefinition.IsDerivedFrom<UnityEngine.Component>())
            {
                Log.Error($"Cannot generate reader for component type {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                WeavingFailed = true;
                return null;
            }
            if (variableReference.Is<UnityEngine.Object>())
            {
                Log.Error($"Cannot generate reader for {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                WeavingFailed = true;
                return null;
            }
            if (variableReference.Is<UnityEngine.ScriptableObject>())
            {
                Log.Error($"Cannot generate reader for {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                WeavingFailed = true;
                return null;
            }
            if (variableDefinition.HasGenericParameters)
            {
                Log.Error($"Cannot generate reader for generic variable {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                WeavingFailed = true;
                return null;
            }
            if (variableDefinition.IsInterface)
            {
                Log.Error($"Cannot generate reader for interface {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                WeavingFailed = true;
                return null;
            }
            if (variableDefinition.IsAbstract)
            {
                Log.Error($"Cannot generate reader for abstract class {variableReference.Name}. Use a supported type or provide a custom reader", variableReference);
                WeavingFailed = true;
                return null;
            }

            return GenerateClassOrStructReadFunction(variableReference, ref WeavingFailed);
        }

        MethodReference GetNetworkBehaviourReader(TypeReference variableReference)
        {
            // uses generic ReadNetworkBehaviour rather than having weaver create one for each NB
            MethodReference generic = weaverTypes.readNetworkBehaviourGeneric;

            MethodReference readFunc = generic.MakeGeneric(assembly.MainModule, variableReference);

            // register function so it is added to Reader<T>
            // use Register instead of RegisterWriteFunc because this is not a generated function
            Register(variableReference, readFunc);

            return readFunc;
        }

        MethodDefinition GenerateEnumReadFunc(TypeReference variable, ref bool WeavingFailed)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            worker.Emit(OpCodes.Ldarg_0);

            TypeReference underlyingType = variable.Resolve().GetEnumUnderlyingType();
            MethodReference underlyingFunc = GetReadFunc(underlyingType, ref WeavingFailed);

            worker.Emit(OpCodes.Call, underlyingFunc);
            worker.Emit(OpCodes.Ret);
            return readerFunc;
        }

        MethodDefinition GenerateArraySegmentReadFunc(TypeReference variable, ref bool WeavingFailed)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];

            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            // $array = reader.Read<[T]>()
            ArrayType arrayType = elementType.MakeArrayType();
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Call, GetReadFunc(arrayType, ref WeavingFailed));

            // return new ArraySegment<T>($array);
            worker.Emit(OpCodes.Newobj, weaverTypes.ArraySegmentConstructorReference.MakeHostInstanceGeneric(assembly.MainModule, genericInstance));
            worker.Emit(OpCodes.Ret);
            return readerFunc;
        }

        MethodDefinition GenerateReaderFunction(TypeReference variable)
        {
            string functionName = "_Read_" + variable.FullName;

            // create new reader for this type
            MethodDefinition readerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    variable);

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, weaverTypes.Import<NetworkReader>()));
            readerFunc.Body.InitLocals = true;
            RegisterReadFunc(variable, readerFunc);

            return readerFunc;
        }

        MethodDefinition GenerateReadCollection(TypeReference variable, TypeReference elementType, string readerFunction, ref bool WeavingFailed)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(variable);
            // generate readers for the element
            GetReadFunc(elementType, ref WeavingFailed);

            ModuleDefinition module = assembly.MainModule;
            TypeReference readerExtensions = module.ImportReference(typeof(NetworkReaderExtensions));
            MethodReference listReader = Resolvers.ResolveMethod(readerExtensions, assembly, Log, readerFunction, ref WeavingFailed);

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

        MethodDefinition GenerateClassOrStructReadFunction(TypeReference variable, ref bool WeavingFailed)
        {
            MethodDefinition readerFunc = GenerateReaderFunction(variable);

            // create local for return value
            readerFunc.Body.Variables.Add(new VariableDefinition(variable));

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            TypeDefinition td = variable.Resolve();

            if (!td.IsValueType)
                GenerateNullCheck(worker, ref WeavingFailed);

            CreateNew(variable, worker, td, ref WeavingFailed);
            ReadAllFields(variable, worker, ref WeavingFailed);

            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ret);
            return readerFunc;
        }

        void GenerateNullCheck(ILProcessor worker, ref bool WeavingFailed)
        {
            // if (!reader.ReadBoolean()) {
            //   return null;
            // }
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Call, GetReadFunc(weaverTypes.Import<bool>(), ref WeavingFailed));

            Instruction labelEmptyArray = worker.Create(OpCodes.Nop);
            worker.Emit(OpCodes.Brtrue, labelEmptyArray);
            // return null
            worker.Emit(OpCodes.Ldnull);
            worker.Emit(OpCodes.Ret);
            worker.Append(labelEmptyArray);
        }

        // Initialize the local variable with a new instance
        void CreateNew(TypeReference variable, ILProcessor worker, TypeDefinition td, ref bool WeavingFailed)
        {
            if (variable.IsValueType)
            {
                // structs are created with Initobj
                worker.Emit(OpCodes.Ldloca, 0);
                worker.Emit(OpCodes.Initobj, variable);
            }
            else if (td.IsDerivedFrom<UnityEngine.ScriptableObject>())
            {
                GenericInstanceMethod genericInstanceMethod = new GenericInstanceMethod(weaverTypes.ScriptableObjectCreateInstanceMethod);
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
                    Log.Error($"{variable.Name} can't be deserialized because it has no default constructor", variable);
                    WeavingFailed = true;
                    return;
                }

                MethodReference ctorRef = assembly.MainModule.ImportReference(ctor);

                worker.Emit(OpCodes.Newobj, ctorRef);
                worker.Emit(OpCodes.Stloc_0);
            }
        }

        void ReadAllFields(TypeReference variable, ILProcessor worker, ref bool WeavingFailed)
        {
            foreach (FieldDefinition field in variable.FindAllPublicFields())
            {
                // mismatched ldloca/ldloc for struct/class combinations is invalid IL, which causes crash at runtime
                OpCode opcode = variable.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc;
                worker.Emit(opcode, 0);
                MethodReference readFunc = GetReadFunc(field.FieldType, ref WeavingFailed);
                if (readFunc != null)
                {
                    worker.Emit(OpCodes.Ldarg_0);
                    worker.Emit(OpCodes.Call, readFunc);
                }
                else
                {
                    Log.Error($"{field.Name} has an unsupported type", field);
                    WeavingFailed = true;
                }
                FieldReference fieldRef = assembly.MainModule.ImportReference(field);

                worker.Emit(OpCodes.Stfld, fieldRef);
            }
        }

        // Save a delegate for each one of the readers into Reader<T>.read
        internal void InitializeReaders(ILProcessor worker)
        {
            ModuleDefinition module = assembly.MainModule;

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
                MethodReference funcConstructorInstance = funcConstructorRef.MakeHostInstanceGeneric(assembly.MainModule, funcGenericInstance);
                worker.Emit(OpCodes.Newobj, funcConstructorInstance);

                // save it in Reader<T>.read
                GenericInstanceType genericInstance = genericReaderClassRef.MakeGenericInstanceType(targetType);
                FieldReference specializedField = fieldRef.SpecializeField(assembly.MainModule, genericInstance);
                worker.Emit(OpCodes.Stsfld, specializedField);
            }
        }
    }
}
