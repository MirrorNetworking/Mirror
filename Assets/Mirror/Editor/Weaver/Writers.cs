using System;
using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;
using Mono.CecilX.Rocks;

namespace Mirror.Weaver
{
    public static class Writers
    {
        static Dictionary<TypeReference, MethodReference> writeFuncs;

        public static void Init()
        {
            writeFuncs = new Dictionary<TypeReference, MethodReference>(new TypeReferenceComparer());
        }

        public static void Register(TypeReference dataType, MethodReference methodReference)
        {
            if (writeFuncs.ContainsKey(dataType))
            {
                Weaver.Warning($"Registering a Write method for {dataType.FullName} when one already exists", methodReference);
            }

            // we need to import type when we Initialize Writers so import here in case it is used anywhere else
            TypeReference imported = Weaver.CurrentAssembly.MainModule.ImportReference(dataType);
            writeFuncs[imported] = methodReference;
        }

        static void RegisterWriteFunc(TypeReference typeReference, MethodDefinition newWriterFunc)
        {
            Register(typeReference, newWriterFunc);

            Weaver.WeaveLists.generateContainerClass.Methods.Add(newWriterFunc);
        }

        /// <summary>
        /// Finds existing writer for type, if non exists trys to create one
        /// <para>This method is recursive</para>
        /// </summary>
        /// <param name="variable"></param>
        /// <returns>Returns <see cref="MethodReference"/> or null</returns>
        public static MethodReference GetWriteFunc(TypeReference variable)
        {
            if (writeFuncs.TryGetValue(variable, out MethodReference foundFunc))
            {
                return foundFunc;
            }
            else
            {
                // this try/catch will be removed in future PR and make `GetWriteFunc` throw instead
                try
                {
                    TypeReference importedVariable = Weaver.CurrentAssembly.MainModule.ImportReference(variable);
                    return GenerateWriter(importedVariable);
                }
                catch (GenerateWriterException e)
                {
                    Weaver.Error(e.Message, e.MemberReference);
                    return null;
                }
            }
        }

        /// <exception cref="GenerateWriterException">Throws when writer could not be generated for type</exception>
        static MethodReference GenerateWriter(TypeReference variableReference)
        {
            if (variableReference.IsByReference)
            {
                throw new GenerateWriterException($"Cannot pass {variableReference.Name} by reference", variableReference);
            }

            // Arrays are special, if we resolve them, we get the element type,
            // e.g. int[] resolves to int
            // therefore process this before checks below
            if (variableReference.IsArray)
            {
                if (variableReference.IsMultidimensionalArray())
                {
                    throw new GenerateWriterException($"{variableReference.Name} is an unsupported type. Multidimensional arrays are not supported", variableReference);
                }
                TypeReference elementType = variableReference.GetElementType();
                return GenerateCollectionWriter(variableReference, elementType, nameof(NetworkWriterExtensions.WriteArray));
            }

            if (variableReference.Resolve()?.IsEnum ?? false)
            {
                // serialize enum as their base type
                return GenerateEnumWriteFunc(variableReference);
            }

            // check for collections
            if (variableReference.Is(typeof(ArraySegment<>)))
            {
                GenericInstanceType genericInstance = (GenericInstanceType)variableReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateCollectionWriter(variableReference, elementType, nameof(NetworkWriterExtensions.WriteArraySegment));
            }
            if (variableReference.Is(typeof(List<>)))
            {
                GenericInstanceType genericInstance = (GenericInstanceType)variableReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateCollectionWriter(variableReference, elementType, nameof(NetworkWriterExtensions.WriteList));
            }

            if (variableReference.IsDerivedFrom<NetworkBehaviour>())
            {
                return GetNetworkBehaviourWriter(variableReference);
            }

            // check for invalid types
            TypeDefinition variableDefinition = variableReference.Resolve();
            if (variableDefinition == null)
            {
                throw new GenerateWriterException($"{variableReference.Name} is not a supported type. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableDefinition.IsDerivedFrom<UnityEngine.Component>())
            {
                throw new GenerateWriterException($"Cannot generate writer for component type {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableReference.Is<UnityEngine.Object>())
            {
                throw new GenerateWriterException($"Cannot generate writer for {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableReference.Is<UnityEngine.ScriptableObject>())
            {
                throw new GenerateWriterException($"Cannot generate writer for {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableDefinition.HasGenericParameters)
            {
                throw new GenerateWriterException($"Cannot generate writer for generic type {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableDefinition.IsInterface)
            {
                throw new GenerateWriterException($"Cannot generate writer for interface {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableDefinition.IsAbstract)
            {
                throw new GenerateWriterException($"Cannot generate writer for abstract class {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }

            // generate writer for class/struct
            return GenerateClassOrStructWriterFunction(variableReference);
        }

        private static MethodReference GetNetworkBehaviourWriter(TypeReference variableReference)
        {
            // all NetworkBehaviours can use the same write function
            if (writeFuncs.TryGetValue(WeaverTypes.Import<NetworkBehaviour>(), out MethodReference func))
            {
                // register function so it is added to writer<T>
                // use Register instead of RegisterWriteFunc because this is not a generated function
                Register(variableReference, func);

                return func;
            }
            else
            {
                // this exception only happens if mirror is missing the WriteNetworkBehaviour method
                throw new MissingMethodException($"Could not find writer for NetworkBehaviour");
            }
        }

        private static MethodDefinition GenerateEnumWriteFunc(TypeReference variable)
        {
            MethodDefinition writerFunc = GenerateWriterFunc(variable);

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            MethodReference underlyingWriter = GetWriteFunc(variable.Resolve().GetEnumUnderlyingType());

            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Call, underlyingWriter));

            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }

        private static MethodDefinition GenerateWriterFunc(TypeReference variable)
        {
            string functionName = "_Write_" + variable.FullName;
            // create new writer for this type
            MethodDefinition writerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    WeaverTypes.Import(typeof(void)));

            writerFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, WeaverTypes.Import<NetworkWriter>()));
            writerFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, variable));
            writerFunc.Body.InitLocals = true;

            RegisterWriteFunc(variable, writerFunc);
            return writerFunc;
        }

        static MethodDefinition GenerateClassOrStructWriterFunction(TypeReference variable)
        {
            MethodDefinition writerFunc = GenerateWriterFunc(variable);

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            if (!variable.Resolve().IsValueType)
                WriteNullCheck(worker);

            if (!WriteAllFields(variable, worker))
                return null;

            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }

        private static void WriteNullCheck(ILProcessor worker)
        {
            // if (value == null)
            // {
            //     writer.WriteBoolean(false);
            //     return;
            // }
            //

            Instruction labelNotNull = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Brtrue, labelNotNull));
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Call, GetWriteFunc(WeaverTypes.Import<bool>())));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(labelNotNull);

            // write.WriteBoolean(true);
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4_1));
            worker.Append(worker.Create(OpCodes.Call, GetWriteFunc(WeaverTypes.Import<bool>())));
        }

        /// <summary>
        /// Find all fields in type and write them
        /// </summary>
        /// <param name="variable"></param>
        /// <param name="worker"></param>
        /// <returns>false if fail</returns>
        static bool WriteAllFields(TypeReference variable, ILProcessor worker)
        {
            uint fields = 0;
            foreach (FieldDefinition field in variable.FindAllPublicFields())
            {
                MethodReference writeFunc = GetWriteFunc(field.FieldType);
                // need this null check till later PR when GetWriteFunc throws exception instead
                if (writeFunc == null) { return false; }

                FieldReference fieldRef = Weaver.CurrentAssembly.MainModule.ImportReference(field);

                fields++;
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldarg_1));
                worker.Append(worker.Create(OpCodes.Ldfld, fieldRef));
                worker.Append(worker.Create(OpCodes.Call, writeFunc));
            }

            return true;
        }

        static MethodDefinition GenerateCollectionWriter(TypeReference variable, TypeReference elementType, string writerFunction)
        {

            MethodDefinition writerFunc = GenerateWriterFunc(variable);

            MethodReference elementWriteFunc = GetWriteFunc(elementType);
            MethodReference intWriterFunc = GetWriteFunc(WeaverTypes.Import<int>());

            // need this null check till later PR when GetWriteFunc throws exception instead
            if (elementWriteFunc == null)
            {
                Weaver.Error($"Cannot generate writer for {variable}. Use a supported type or provide a custom writer", variable);
                return writerFunc;
            }

            ModuleDefinition module = Weaver.CurrentAssembly.MainModule;
            TypeReference readerExtensions = module.ImportReference(typeof(NetworkWriterExtensions));
            MethodReference collectionWriter = Resolvers.ResolveMethod(readerExtensions, Weaver.CurrentAssembly, writerFunction);

            GenericInstanceMethod methodRef = new GenericInstanceMethod(collectionWriter);
            methodRef.GenericArguments.Add(elementType);

            // generates
            // reader.WriteArray<T>(array);

            ILProcessor worker = writerFunc.Body.GetILProcessor();
            worker.Append(worker.Create(OpCodes.Ldarg_0)); // writer
            worker.Append(worker.Create(OpCodes.Ldarg_1)); // collection

            worker.Append(worker.Create(OpCodes.Call, methodRef)); // WriteArray

            worker.Append(worker.Create(OpCodes.Ret));

            return writerFunc;
        }

        /// <summary>
        /// Save a delegate for each one of the writers into <see cref="Writer{T}.write"/>
        /// </summary>
        /// <param name="worker"></param>
        internal static void InitializeWriters(ILProcessor worker)
        {
            ModuleDefinition module = Weaver.CurrentAssembly.MainModule;

            TypeReference genericWriterClassRef = module.ImportReference(typeof(Writer<>));

            System.Reflection.FieldInfo fieldInfo = typeof(Writer<>).GetField(nameof(Writer<object>.write));
            FieldReference fieldRef = module.ImportReference(fieldInfo);
            TypeReference networkWriterRef = module.ImportReference(typeof(NetworkWriter));
            TypeReference actionRef = module.ImportReference(typeof(Action<,>));
            MethodReference actionConstructorRef = module.ImportReference(typeof(Action<,>).GetConstructors()[0]);

            foreach (KeyValuePair<TypeReference, MethodReference> kvp in writeFuncs)
            {
                TypeReference targetType = kvp.Key;
                MethodReference writeFunc = kvp.Value;

                // create a Action<NetworkWriter, T> delegate
                worker.Append(worker.Create(OpCodes.Ldnull));
                worker.Append(worker.Create(OpCodes.Ldftn, writeFunc));
                GenericInstanceType actionGenericInstance = actionRef.MakeGenericInstanceType(networkWriterRef, targetType);
                MethodReference actionRefInstance = actionConstructorRef.MakeHostInstanceGeneric(actionGenericInstance);
                worker.Append(worker.Create(OpCodes.Newobj, actionRefInstance));

                // save it in Writer<T>.write
                GenericInstanceType genericInstance = genericWriterClassRef.MakeGenericInstanceType(targetType);
                FieldReference specializedField = fieldRef.SpecializeField(genericInstance);
                worker.Append(worker.Create(OpCodes.Stsfld, specializedField));
            }
        }

    }
}
