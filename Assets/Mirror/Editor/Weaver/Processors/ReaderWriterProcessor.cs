// finds all readers and writers and register them
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Mirror.Weaver
{
    class TypeReferenceComparer : IEqualityComparer<TypeReference>
    {
        public bool Equals(TypeReference x, TypeReference y)
        {
            return x.FullName == y.FullName;
        }

        public int GetHashCode(TypeReference obj)
        {
            return obj.FullName.GetHashCode();
        }
    }

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

        public bool Process(Assembly unityAssembly)
        {
            // darn global state causing bugs
            foreach (Assembly unityAsm in unityAssembly.assemblyReferences)
            {
                if (unityAsm.name == "Mirror")
                {
                    using (var asmResolver = new DefaultAssemblyResolver())
                    using (var assembly = AssemblyDefinition.ReadAssembly(unityAsm.outputPath, new ReaderParameters { ReadWrite = false, ReadSymbols = false, AssemblyResolver = asmResolver }))
                    {
                        ProcessAssemblyClasses(assembly.MainModule);
                    }
                }
            }

            int writeCount = writers.Count;
            int readCount = readers.Count;

            ProcessAssemblyClasses(module);

            return writers.Count != writeCount || readers.Count != readCount;
        }

        void ProcessAssemblyClasses(ModuleDefinition dependencyModule)
        {
            foreach (TypeDefinition klass in dependencyModule.Types)
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
            CodePass.ForEachInstruction(dependencyModule, (md, instr) => GenerateReadersWriters(instr));
        }

        private Instruction GenerateReadersWriters(Instruction instruction)
        {
            if (instruction.OpCode == OpCodes.Ldsfld)
            {
                GenerateReadersWriters((FieldReference)instruction.Operand);
            }

            // We are looking for calls to some specific types
            if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
            {
                GenerateReadersWriters((MethodReference)instruction.Operand);
            }

            return instruction;
        }

        private void GenerateReadersWriters(FieldReference field)
        {
            TypeReference type = field.DeclaringType;

            if (type.Is(typeof(Writer<>)) || type.Is(typeof(Reader<>)) && type.IsGenericInstance)
            {
                var typeGenericInstance = (GenericInstanceType)type;

                TypeReference parameterType = typeGenericInstance.GenericArguments[0];

                GenerateReadersWriters(parameterType);
            }
        }

        private void GenerateReadersWriters(MethodReference method)
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

                GenerateReadersWriters(parameterType);
                if (isMessage)
                    messages.Add(parameterType);
            }
        }

        private void GenerateReadersWriters(TypeReference parameterType)
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

                writers.GetWriteFunc(parameterType);
                readers.GetReadFunc(parameterType);
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
                assemblyReference.Name == nameof(UnityEditor)
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
                MethodAttributes.Public | MethodAttributes.Static);

            System.Reflection.ConstructorInfo attributeconstructor = typeof(RuntimeInitializeOnLoadMethodAttribute).GetConstructor(new [] { typeof(RuntimeInitializeLoadType)});

            var customAttributeRef = new CustomAttribute(module.ImportReference(attributeconstructor));
            customAttributeRef.ConstructorArguments.Add(new CustomAttributeArgument(module.ImportReference<RuntimeInitializeLoadType>(), RuntimeInitializeLoadType.BeforeSceneLoad));
            rwInitializer.CustomAttributes.Add(customAttributeRef);

            if (IsEditorAssembly(module))
            {
                // editor assembly,  add InitializeOnLoadMethod too.  Useful for the editor tests
                System.Reflection.ConstructorInfo initializeOnLoadConstructor = typeof(InitializeOnLoadMethodAttribute).GetConstructor(new Type[0]);
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
            System.Reflection.MethodInfo method = typeof(MessagePacker).GetMethod(nameof(MessagePacker.RegisterMessage));
            MethodReference registerMethod = module.ImportReference(method);

            foreach (TypeReference message in messages)
            {
                var genericMethodCall = new GenericInstanceMethod(registerMethod);
                genericMethodCall.GenericArguments.Add(module.ImportReference(message));
                worker.Append(worker.Create(OpCodes.Call, genericMethodCall));
            }
        }
    }
}
