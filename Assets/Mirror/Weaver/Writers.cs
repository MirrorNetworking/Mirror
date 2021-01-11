using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Mirror.Weaver
{
    public class Writers
    {
        readonly Dictionary<string, MethodReference> writeFuncs = new Dictionary<string, MethodReference>();

        public int Count => writeFuncs.Count;

        private readonly IWeaverLogger logger;
        private readonly ModuleDefinition module;

        public Writers(ModuleDefinition module, IWeaverLogger logger)
        {
            this.logger = logger;
            this.module = module;
        }

        public void Register(TypeReference dataType, MethodReference methodReference)
        {
            writeFuncs[dataType.FullName] = methodReference;
        }

        void RegisterWriteFunc(TypeReference typeReference, MethodDefinition newWriterFunc)
        {
            writeFuncs[typeReference.FullName] = newWriterFunc;
        }

        public MethodReference GetWriteFunc<T>() =>
            GetWriteFunc(module.ImportReference<T>());
       
        /// <summary>
        /// Finds existing writer for type, if non exists trys to create one
        /// <para>This method is recursive</para>
        /// </summary>
        /// <param name="typeReference"></param>
        /// <param name="recursionCount"></param>
        /// <returns>Returns <see cref="MethodReference"/> or null</returns>
        public MethodReference GetWriteFunc(TypeReference typeReference)
        {
            if (writeFuncs.TryGetValue(typeReference.FullName, out MethodReference foundFunc))
            {
                return foundFunc;
            }
            else
            {
                // this try/catch will be removed in future PR and make `GetWriteFunc` throw instead
                try
                {   
                    return GenerateWriter(module.ImportReference(typeReference));
                }
                catch (GenerateWriterException e)
                {
                    logger.Error(e.Message, e.MemberReference);
                    return null;
                }
            }
        }

        /// <exception cref="GenerateWriterException">Throws when writer could not be generated for type</exception>
        MethodDefinition GenerateWriter(TypeReference typeReference)
        {
            if (typeReference.IsByReference)
            {
                throw new GenerateWriterException($"Cannot pass {typeReference.Name} by reference", typeReference);
            }

            // Arrays are special, if we resolve them, we get the element type,
            // eg int[] resolves to int
            // therefore process this before checks below
            if (typeReference.IsArray)
            {
                if (typeReference.IsMultidimensionalArray())
                {
                    throw new GenerateWriterException($"{typeReference.Name} is an unsupported type. Multidimensional arrays are not supported", typeReference);
                }
                TypeReference elementType = typeReference.GetElementType();
                return GenerateCollectionWriter(typeReference, elementType, () => NetworkWriterExtensions.WriteArray<byte>(default, default));
            }

            if (typeReference.Resolve()?.IsEnum ?? false)
            {
                // serialize enum as their base type
                return GenerateEnumWriteFunc(typeReference);
            }

            // check for collections
            if (typeReference.Is(typeof(ArraySegment<>)))
            {
                var genericInstance = (GenericInstanceType)typeReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateCollectionWriter(typeReference, elementType, () => NetworkWriterExtensions.WriteArraySegment<byte>(default, default));
            }
            if (typeReference.Is(typeof(List<>)))
            {
                var genericInstance = (GenericInstanceType)typeReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateCollectionWriter(typeReference, elementType, () => NetworkWriterExtensions.WriteList<byte>(default, default));
            }

            // check for invalid types
            TypeDefinition typeDefinition = typeReference.Resolve();
            if (typeDefinition == null)
            {
                throw new GenerateWriterException($"{typeReference.Name} is not a supported type. Use a supported type or provide a custom writer", typeReference);
            }
            if (typeDefinition.IsDerivedFrom<UnityEngine.Component>())
            {
                throw new GenerateWriterException($"Cannot generate writer for component type {typeReference.Name}. Use a supported type or provide a custom writer", typeReference);
            }
            if (typeReference.Is<UnityEngine.Object>())
            {
                throw new GenerateWriterException($"Cannot generate writer for {typeReference.Name}. Use a supported type or provide a custom writer", typeReference);
            }
            if (typeReference.Is<UnityEngine.ScriptableObject>())
            {
                throw new GenerateWriterException($"Cannot generate writer for {typeReference.Name}. Use a supported type or provide a custom writer", typeReference);
            }
            if (typeReference.Is<UnityEngine.GameObject>())
            {
                throw new GenerateWriterException($"Cannot generate writer for {typeReference.Name}. Use a supported type or provide a custom writer", typeReference);
            }
            if (typeDefinition.HasGenericParameters)
            {
                throw new GenerateWriterException($"Cannot generate writer for generic type {typeReference.Name}. Use a supported type or provide a custom writer", typeReference);
            }
            if (typeDefinition.IsInterface)
            {
                throw new GenerateWriterException($"Cannot generate writer for interface {typeReference.Name}. Use a supported type or provide a custom writer", typeReference);
            }
            if (typeDefinition.IsAbstract)
            {
                throw new GenerateWriterException($"Cannot generate writer for abstract class {typeReference.Name}. Use a supported type or provide a custom writer", typeReference);
            }

            // generate writer for class/struct
            return GenerateClassOrStructWriterFunction(typeReference);
        }

        private MethodDefinition GenerateEnumWriteFunc(TypeReference typeReference)
        {
            MethodDefinition writerFunc = GenerateWriterFunc(typeReference);

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            MethodReference underlyingWriter = GetWriteFunc(typeReference.Resolve().GetEnumUnderlyingType());

            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Call, underlyingWriter));

            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }

        private MethodDefinition GenerateWriterFunc(TypeReference typeReference)
        {
            string functionName = "_Write_" + typeReference.FullName;
            // create new writer for this type
            MethodDefinition writerFunc = module.GeneratedClass().AddMethod(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig);

            _ = writerFunc.AddParam<NetworkWriter>("writer");
            _ = writerFunc.AddParam(typeReference, "value");
            writerFunc.Body.InitLocals = true;

            RegisterWriteFunc(typeReference, writerFunc);
            return writerFunc;
        }

        MethodDefinition GenerateClassOrStructWriterFunction(TypeReference typeReference)
        {
            MethodDefinition writerFunc = GenerateWriterFunc(typeReference);

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            if (!typeReference.Resolve().IsValueType)
                WriteNullCheck(worker);

            if (!WriteAllFields(typeReference, worker))
                return writerFunc;

            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }

        private void WriteNullCheck(ILProcessor worker)
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
            worker.Append(worker.Create(OpCodes.Call,  GetWriteFunc<bool>()));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(labelNotNull);

            // write.WriteBoolean(true);
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4_1));
            worker.Append(worker.Create(OpCodes.Call, GetWriteFunc<bool>()));
        }

        /// <summary>
        /// Find all fields in type and write them
        /// </summary>
        /// <param name="variable"></param>
        /// <param name="worker"></param>
        /// <returns>false if fail</returns>
        bool WriteAllFields(TypeReference variable, ILProcessor worker)
        {
            uint fields = 0;
            foreach (FieldDefinition field in variable.FindAllPublicFields())
            {
                MethodReference writeFunc = GetWriteFunc(field.FieldType);
                // need this null check till later PR when GetWriteFunc throws exception instead
                if (writeFunc == null) { return false; }

                FieldReference fieldRef = module.ImportReference(field);

                fields++;
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldarg_1));
                worker.Append(worker.Create(OpCodes.Ldfld, fieldRef));
                worker.Append(worker.Create(OpCodes.Call, writeFunc));
            }

            return true;
        }

        MethodDefinition GenerateCollectionWriter(TypeReference variable, TypeReference elementType, Expression<Action> writerFunction)
        {
           
            MethodDefinition writerFunc = GenerateWriterFunc(variable);

            MethodReference elementWriteFunc = GetWriteFunc(elementType);

            // need this null check till later PR when GetWriteFunc throws exception instead
            if (elementWriteFunc == null)
            {
                logger.Error($"Cannot generate writer for {variable}. Use a supported type or provide a custom writer", variable);
                return writerFunc;
            }

            MethodReference collectionWriter = module.ImportReference(writerFunction).GetElementMethod();

            var methodRef = new GenericInstanceMethod(collectionWriter);
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
        /// Save a delegate for each one of the writers into <see cref="Writer{T}.Write"/>
        /// </summary>
        /// <param name="worker"></param>
        internal void InitializeWriters(ILProcessor worker)
        {
            TypeReference genericWriterClassRef = module.ImportReference(typeof(Writer<>));

            System.Reflection.PropertyInfo writerProperty = typeof(Writer<>).GetProperty(nameof(Writer<int>.Write));
            MethodReference fieldRef = module.ImportReference(writerProperty.GetSetMethod());
            TypeReference networkWriterRef = module.ImportReference(typeof(NetworkWriter));
            TypeReference actionRef = module.ImportReference(typeof(Action<,>));
            MethodReference actionConstructorRef = module.ImportReference(typeof(Action<,>).GetConstructors()[0]);

            foreach (MethodReference writerMethod in writeFuncs.Values)
            {

                TypeReference dataType = writerMethod.Parameters[1].ParameterType;

                // create a Action<NetworkWriter, T> delegate
                worker.Append(worker.Create(OpCodes.Ldnull));
                worker.Append(worker.Create(OpCodes.Ldftn, writerMethod));
                GenericInstanceType actionGenericInstance = actionRef.MakeGenericInstanceType(networkWriterRef, dataType);
                MethodReference actionRefInstance = actionConstructorRef.MakeHostInstanceGeneric(actionGenericInstance);
                worker.Append(worker.Create(OpCodes.Newobj, actionRefInstance));

                // save it in Writer<T>.write
                GenericInstanceType genericInstance = genericWriterClassRef.MakeGenericInstanceType(dataType);
                MethodReference specializedField = fieldRef.MakeHostInstanceGeneric(genericInstance);
                worker.Append(worker.Create(OpCodes.Call, specializedField));
            }
        }
    }
}
