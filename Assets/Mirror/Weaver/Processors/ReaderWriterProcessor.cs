// finds all readers and writers and register them
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;
using UnityEngine;

namespace Mirror.Weaver
{
    public class ReaderWriterProcessor
    {
        private readonly HashSet<TypeReference> messages = new HashSet<TypeReference>(new TypeReferenceComparer());

        private readonly ModuleDefinition module;
        private readonly Readers readers;
        private readonly Writers writers;

        public ReaderWriterProcessor(ModuleDefinition module, Readers readers, Writers writers)
        {
            this.module = module;
            this.readers = readers;
            this.writers = writers;
        }

        public bool Process()
        {
            messages.Clear();


            LoadBuiltInReadersAndWriters();

            int writeCount = writers.Count;
            int readCount = readers.Count;

            ProcessAssemblyClasses();

            return writers.Count != writeCount || readers.Count != readCount;
        }

        private static bool IsExtension(MethodInfo method) => Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.ExtensionAttribute));

        #region Load MirrorNG built in readers and writers
        private void LoadBuiltInReadersAndWriters()
        {
            // find all extension methods
            IEnumerable<Type> types = typeof(NetworkReaderExtensions).Module.GetTypes().Where(t => t.IsSealed && t.IsAbstract);
            foreach (Type type in types)
            {
                IEnumerable<MethodInfo> methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Where(IsExtension)
                    .Where(m => !m.IsGenericMethod);

                foreach (MethodInfo method in methods)
                {
                    RegisterReader(method);
                    RegisterWriter(method);
                }
            }
        }

        private void RegisterReader(MethodInfo method)
        {
            if (method.GetParameters().Length != 1)
                return;

            if (method.GetParameters()[0].ParameterType.FullName != typeof(NetworkReader).FullName)
                return;

            if (method.ReturnType == typeof(void))
                return;
            readers.Register(module.ImportReference(method.ReturnType), module.ImportReference(method));
        }

        private void RegisterWriter(MethodInfo method)
        {
            if (method.GetParameters().Length != 2)
                return;

            if (method.GetParameters()[0].ParameterType.FullName != typeof(NetworkWriter).FullName)
                return;

            if (method.ReturnType != typeof(void))
                return;

            Type dataType = method.GetParameters()[1].ParameterType;
            writers.Register(module.ImportReference(dataType), module.ImportReference(method));
        }
        #endregion

        #region Assembly defined reader/writer
        void ProcessAssemblyClasses()
        {
            var types = new List<TypeDefinition>(module.Types);

            foreach (TypeDefinition klass in types)
            {
                // extension methods only live in static classes
                // static classes are represented as sealed and abstract
                if (klass.IsAbstract && klass.IsSealed)
                {
                    LoadDeclaredWriters(klass);
                    LoadDeclaredReaders(klass);
                }
            }

            // Generate readers and writers
            // find all the Send<> and Register<> calls and generate
            // readers and writers for them.
            CodePass.ForEachInstruction(module, (md, instr, sequencePoint) => GenerateReadersWriters(instr, sequencePoint));
        }

        private Instruction GenerateReadersWriters(Instruction instruction, SequencePoint sequencePoint)
        {
            if (instruction.OpCode == OpCodes.Ldsfld)
            {
                GenerateReadersWriters((FieldReference)instruction.Operand, sequencePoint);
            }

            // We are looking for calls to some specific types
            if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
            {
                GenerateReadersWriters((MethodReference)instruction.Operand, sequencePoint);
            }

            return instruction;
        }

        private void GenerateReadersWriters(FieldReference field, SequencePoint sequencePoint)
        {
            TypeReference type = field.DeclaringType;

            if (type.Is(typeof(Writer<>)) || type.Is(typeof(Reader<>)) && type.IsGenericInstance)
            {
                var typeGenericInstance = (GenericInstanceType)type;

                TypeReference parameterType = typeGenericInstance.GenericArguments[0];

                GenerateReadersWriters(parameterType, sequencePoint);
            }
        }

        private void GenerateReadersWriters(MethodReference method, SequencePoint sequencePoint)
        {
            if (!method.IsGenericInstance)
                return;

            bool isMessage =
                method.Is(typeof(MessagePacker), nameof(MessagePacker.Pack)) ||
                method.Is(typeof(MessagePacker), nameof(MessagePacker.GetId)) ||
                method.Is(typeof(MessagePacker), nameof(MessagePacker.Unpack)) ||
                method.Is<IMessageHandler>(nameof(IMessageHandler.Send)) ||
                method.Is<IMessageHandler>(nameof(IMessageHandler.SendAsync)) ||
                method.Is<IMessageHandler>(nameof(IMessageHandler.RegisterHandler)) ||
                method.Is<IMessageHandler>(nameof(IMessageHandler.UnregisterHandler)) ||
                method.Is<IMessageHandler>(nameof(IMessageHandler.Send)) ||
                method.Is<NetworkConnection>(nameof(NetworkConnection.Send)) ||
                method.Is<NetworkConnection>(nameof(NetworkConnection.SendAsync)) ||
                method.Is<NetworkConnection>(nameof(NetworkConnection.RegisterHandler)) ||
                method.Is<NetworkConnection>(nameof(NetworkConnection.UnregisterHandler)) ||
                method.Is<INetworkClient>(nameof(INetworkClient.Send)) ||
                method.Is<INetworkClient>(nameof(INetworkClient.SendAsync)) ||
                method.Is<NetworkClient>(nameof(NetworkClient.Send)) ||
                method.Is<NetworkClient>(nameof(NetworkClient.SendAsync)) ||
                method.Is<NetworkServer>(nameof(NetworkServer.SendToAll)) ||
                method.Is<NetworkServer>(nameof(NetworkServer.SendToClientOfPlayer)) ||
                method.Is<INetworkServer>(nameof(INetworkServer.SendToAll));

            bool generate = isMessage ||
                method.Is<NetworkWriter>(nameof(NetworkWriter.Write)) ||
                method.Is<NetworkReader>(nameof(NetworkReader.Read));

            if (generate)
            {
                var instanceMethod = (GenericInstanceMethod)method;
                TypeReference parameterType = instanceMethod.GenericArguments[0];

                if (parameterType.IsGenericParameter)
                    return;

                GenerateReadersWriters(parameterType, sequencePoint);
                if (isMessage)
                    messages.Add(parameterType);
            }
        }

        private void GenerateReadersWriters(TypeReference parameterType, SequencePoint sequencePoint)
        {
            if (!parameterType.IsGenericParameter && parameterType.CanBeResolved())
            {
                TypeDefinition typeDefinition = parameterType.Resolve();

                if (typeDefinition.IsClass && !typeDefinition.IsValueType)
                {
                    MethodDefinition constructor = typeDefinition.GetMethod(".ctor");

                    bool hasAccess = constructor.IsPublic
                        || constructor.IsAssembly && typeDefinition.Module == module;

                    if (!hasAccess)
                        return;
                }

                writers.GetWriteFunc(parameterType, sequencePoint);
                readers.GetReadFunc(parameterType, sequencePoint);
            }
        }

        void LoadDeclaredWriters(TypeDefinition klass)
        {
            // register all the writers in this class.  Skip the ones with wrong signature
            foreach (MethodDefinition method in klass.Methods)
            {
                if (method.Parameters.Count != 2)
                    continue;

                if (!method.Parameters[0].ParameterType.Is<NetworkWriter>())
                    continue;

                if (!method.ReturnType.Is(typeof(void)))
                    continue;

                if (!method.HasCustomAttribute<System.Runtime.CompilerServices.ExtensionAttribute>())
                    continue;

                if (method.HasGenericParameters)
                    continue;

                TypeReference dataType = method.Parameters[1].ParameterType;
                writers.Register(dataType, module.ImportReference(method));
            }
        }

        void LoadDeclaredReaders( TypeDefinition klass)
        {
            // register all the reader in this class.  Skip the ones with wrong signature
            foreach (MethodDefinition method in klass.Methods)
            {
                if (method.Parameters.Count != 1)
                    continue;

                if (!method.Parameters[0].ParameterType.Is<NetworkReader>())
                    continue;

                if (method.ReturnType.Is(typeof(void)))
                    continue;

                if (!method.HasCustomAttribute<System.Runtime.CompilerServices.ExtensionAttribute>())
                    continue;

                if (method.HasGenericParameters)
                    continue;

                readers.Register(method.ReturnType, module.ImportReference(method));
            }
        }

        private static bool IsEditorAssembly(ModuleDefinition module)
        {
            return module.AssemblyReferences.Any(assemblyReference =>
                assemblyReference.Name == "Mirror.Editor"
                ) ;
        }

        /// <summary>
        /// Creates a method that will store all the readers and writers into
        /// <see cref="Writer{T}.Write"/> and <see cref="Reader{T}.Read"/>
        ///
        /// The method will be marked InitializeOnLoadMethodAttribute so it gets
        /// executed before mirror runtime code
        /// </summary>
        /// <param name="currentAssembly"></param>
        public void InitializeReaderAndWriters()
        {
            MethodDefinition rwInitializer = module.GeneratedClass().AddMethod(
                "InitReadWriters",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static);

            ConstructorInfo attributeconstructor = typeof(RuntimeInitializeOnLoadMethodAttribute).GetConstructor(new [] { typeof(RuntimeInitializeLoadType)});

            var customAttributeRef = new CustomAttribute(module.ImportReference(attributeconstructor));
            customAttributeRef.ConstructorArguments.Add(new CustomAttributeArgument(module.ImportReference<RuntimeInitializeLoadType>(), RuntimeInitializeLoadType.BeforeSceneLoad));
            rwInitializer.CustomAttributes.Add(customAttributeRef);

            if (IsEditorAssembly(module))
            {
                // editor assembly,  add InitializeOnLoadMethod too.  Useful for the editor tests
                ConstructorInfo initializeOnLoadConstructor = typeof(InitializeOnLoadMethodAttribute).GetConstructor(new Type[0]);
                var initializeCustomConstructorRef = new CustomAttribute(module.ImportReference(initializeOnLoadConstructor));
                rwInitializer.CustomAttributes.Add(initializeCustomConstructorRef);
            }

            ILProcessor worker = rwInitializer.Body.GetILProcessor();

            writers.InitializeWriters(worker);
            readers.InitializeReaders(worker);

            RegisterMessages(worker);

            worker.Append(worker.Create(OpCodes.Ret));
        }

        private void RegisterMessages(ILProcessor worker)
        {
            MethodInfo method = typeof(MessagePacker).GetMethod(nameof(MessagePacker.RegisterMessage));
            MethodReference registerMethod = module.ImportReference(method);

            foreach (TypeReference message in messages)
            {
                var genericMethodCall = new GenericInstanceMethod(registerMethod);
                genericMethodCall.GenericArguments.Add(module.ImportReference(message));
                worker.Append(worker.Create(OpCodes.Call, genericMethodCall));
            }
        }

        #endregion
    }
}
