using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
using Mono.Cecil.Pdb;
using Mono.Cecil.Mdb;

namespace Mirror.Weaver
{
    // This data is flushed each time - if we are run multiple times in the same process/domain
    class WeaverLists
    {
        // setter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldDefinition, MethodDefinition> replacementSetterProperties = new Dictionary<FieldDefinition, MethodDefinition>();
        // getter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldDefinition, MethodDefinition> replacementGetterProperties = new Dictionary<FieldDefinition, MethodDefinition>();

        // [Command]/[ClientRpc] functions that should be replaced. dict<originalMethodFullName, replacement>
        public Dictionary<string, MethodDefinition> replaceMethods = new Dictionary<string, MethodDefinition>();

        // [SyncEvent] invoke functions that should be replaced. dict<originalEventName, replacement>
        public Dictionary<string, MethodDefinition> replaceEvents = new Dictionary<string, MethodDefinition>();

        public Dictionary<string, MethodReference> readFuncs;
        public Dictionary<string, MethodReference> writeFuncs;

        public List<MethodDefinition> generatedReadFunctions = new List<MethodDefinition>();
        public List<MethodDefinition> generatedWriteFunctions = new List<MethodDefinition>();

        public TypeDefinition generateContainerClass;

        // amount of SyncVars per class. dict<className, amount>
        public Dictionary<string, int> numSyncVars = new Dictionary<string, int>();
    }

    class Weaver
    {
        public static WeaverLists WeaveLists { get; private set; }
        public static AssemblyDefinition CurrentAssembly { get; private set; }
        public static ModuleDefinition CorLibModule { get; private set; }
        public static AssemblyDefinition UnityAssembly { get; private set; }
        public static AssemblyDefinition NetAssembly { get; private set; }
        public static bool WeavingFailed { get; private set; }
        public static bool GenerateLogErrors { get; set; }

        // private properties
        static bool DebugLogEnabled = true;

        // this is used to prevent stack overflows when generating serialization code when there are self-referencing types.
        // All the utility classes use GetWriteFunc() to generate serialization code, so the recursion check is implemented there instead of in each utility class.
        // A NetworkBehaviour with the max SyncVars (64) can legitimately increment this value to 65 - so max must be higher than that
        const int MaxRecursionCount = 128;
        static int RecursionCount;

        // Network types
        public static TypeReference NetworkBehaviourType;
        public static TypeReference NetworkBehaviourType2;
        public static TypeReference MonoBehaviourType;
        public static TypeReference ScriptableObjectType;
        public static TypeReference NetworkConnectionType;

        public static TypeReference MessageBaseType;
        public static TypeReference SyncListType;
        public static TypeReference SyncSetType;
        public static TypeReference SyncDictionaryType;

        public static MethodReference NetworkBehaviourDirtyBitsReference;
        public static TypeReference NetworkClientType;
        public static TypeReference NetworkServerType;

        public static TypeReference NetworkReaderType;
        public static TypeDefinition NetworkReaderDef;

        public static TypeReference NetworkWriterType;
        public static TypeDefinition NetworkWriterDef;

        public static MethodReference NetworkWriterCtor;
        public static MethodReference NetworkReaderCtor;
        public static MethodReference getComponentReference;
        public static MethodReference getNetIdReference;
        public static TypeReference NetworkIdentityType;
        public static TypeReference IEnumeratorType;

        public static TypeReference ClientSceneType;
        public static MethodReference ReadyConnectionReference;

        public static TypeReference ComponentType;

        public static TypeReference CmdDelegateReference;
        public static MethodReference CmdDelegateConstructor;

        public static MethodReference NetworkReaderReadInt32;

        public static MethodReference NetworkWriterWriteInt32;
        public static MethodReference NetworkWriterWriteInt16;

        public static MethodReference NetworkServerGetActive;
        public static MethodReference NetworkServerGetLocalClientActive;
        public static MethodReference NetworkClientGetActive;
        public static MethodReference UBehaviourIsServer;
        public static MethodReference NetworkReaderReadPackedUInt32;
        public static MethodReference NetworkReaderReadPackedInt32;
        public static MethodReference NetworkReaderReadPackedUInt64;
        public static MethodReference NetworkReaderReadPackedInt64;
        public static MethodReference NetworkReaderReadByte;
        public static MethodReference NetworkWriterWritePackedUInt32;
        public static MethodReference NetworkWriterWritePackedInt32;
        public static MethodReference NetworkWriterWritePackedUInt64;
        public static MethodReference NetworkWriterWritePackedInt64;

        public static MethodReference NetworkReadUInt16;
        public static MethodReference NetworkWriteUInt16;

        // custom attribute types
        public static TypeReference SyncVarType;
        public static TypeReference CommandType;
        public static TypeReference ClientRpcType;
        public static TypeReference TargetRpcType;
        public static TypeReference SyncEventType;
        public static TypeReference SyncObjectType;
        public static MethodReference InitSyncObjectReference;

        // system types
        public static TypeReference voidType;
        public static TypeReference singleType;
        public static TypeReference doubleType;
        public static TypeReference decimalType;
        public static TypeReference boolType;
        public static TypeReference stringType;
        public static TypeReference int64Type;
        public static TypeReference uint64Type;
        public static TypeReference int32Type;
        public static TypeReference uint32Type;
        public static TypeReference int16Type;
        public static TypeReference uint16Type;
        public static TypeReference byteType;
        public static TypeReference sbyteType;
        public static TypeReference charType;
        public static TypeReference objectType;
        public static TypeReference valueTypeType;
        public static TypeReference vector2Type;
        public static TypeReference vector3Type;
        public static TypeReference vector4Type;
        public static TypeReference vector2IntType;
        public static TypeReference vector3IntType;
        public static TypeReference colorType;
        public static TypeReference color32Type;
        public static TypeReference quaternionType;
        public static TypeReference rectType;
        public static TypeReference rayType;
        public static TypeReference planeType;
        public static TypeReference matrixType;
        public static TypeReference guidType;
        public static TypeReference typeType;
        public static TypeReference gameObjectType;
        public static TypeReference transformType;
        public static TypeReference unityObjectType;
        public static MethodReference gameObjectInequality;

        public static MethodReference setSyncVarReference;
        public static MethodReference setSyncVarHookGuard;
        public static MethodReference getSyncVarHookGuard;
        public static MethodReference setSyncVarGameObjectReference;
        public static MethodReference getSyncVarGameObjectReference;
        public static MethodReference setSyncVarNetworkIdentityReference;
        public static MethodReference getSyncVarNetworkIdentityReference;
        public static MethodReference registerCommandDelegateReference;
        public static MethodReference registerRpcDelegateReference;
        public static MethodReference registerEventDelegateReference;
        public static MethodReference getTypeReference;
        public static MethodReference getTypeFromHandleReference;
        public static MethodReference logErrorReference;
        public static MethodReference logWarningReference;
        public static MethodReference sendCommandInternal;
        public static MethodReference sendRpcInternal;
        public static MethodReference sendTargetRpcInternal;
        public static MethodReference sendEventInternal;

        public static void ResetRecursionCount()
        {
            RecursionCount = 0;
        }

        public static void DLog(TypeDefinition td, string fmt, params object[] args)
        {
            if (!DebugLogEnabled)
                return;

            Console.WriteLine("[" + td.Name + "] " + String.Format(fmt, args));
        }

        // display weaver error
        // and mark process as failed
        public static void Error(string message)
        {
            Log.Error(message);
            WeavingFailed = true;
        }

        public static int GetSyncVarStart(string className)
        {
            return WeaveLists.numSyncVars.ContainsKey(className)
                   ? WeaveLists.numSyncVars[className]
                   : 0;
        }

        public static void SetNumSyncVars(string className, int num)
        {
            WeaveLists.numSyncVars[className] = num;
        }

        public static MethodReference GetWriteFunc(TypeReference variable)
        {
            if (RecursionCount++ > MaxRecursionCount)
            {
                Error("GetWriteFunc recursion depth exceeded for " + variable.Name + ". Check for self-referencing member variables.");
                return null;
            }

            if (WeaveLists.writeFuncs.ContainsKey(variable.FullName))
            {
                MethodReference foundFunc = WeaveLists.writeFuncs[variable.FullName];
                if (foundFunc.Parameters[0].ParameterType.IsArray == variable.IsArray)
                {
                    return foundFunc;
                }
            }

            if (variable.IsByReference)
            {
                // error??
                Error("GetWriteFunc variable.IsByReference error.");
                return null;
            }

            MethodDefinition newWriterFunc;

            if (variable.IsArray)
            {
                TypeReference elementType = variable.GetElementType();
                MethodReference elemenWriteFunc = GetWriteFunc(elementType);
                if (elemenWriteFunc == null)
                {
                    return null;
                }
                newWriterFunc = GenerateArrayWriteFunc(variable, elemenWriteFunc);
            }
            else
            {
                if (variable.Resolve().IsEnum)
                {
                    return NetworkWriterWriteInt32;
                }

                newWriterFunc = GenerateWriterFunction(variable);
            }

            if (newWriterFunc == null)
            {
                return null;
            }

            RegisterWriteFunc(variable.FullName, newWriterFunc);
            return newWriterFunc;
        }

        public static void RegisterWriteFunc(string name, MethodDefinition newWriterFunc)
        {
            WeaveLists.writeFuncs[name] = newWriterFunc;
            WeaveLists.generatedWriteFunctions.Add(newWriterFunc);

            ConfirmGeneratedCodeClass(CurrentAssembly.MainModule);
            WeaveLists.generateContainerClass.Methods.Add(newWriterFunc);
        }

        public static MethodReference GetReadFunc(TypeReference variable)
        {
            if (WeaveLists.readFuncs.ContainsKey(variable.FullName))
            {
                MethodReference foundFunc = WeaveLists.readFuncs[variable.FullName];
                if (foundFunc.ReturnType.IsArray == variable.IsArray)
                {
                    return foundFunc;
                }
            }

            TypeDefinition td = variable.Resolve();
            if (td == null)
            {
                Error("GetReadFunc unsupported type " + variable.FullName);
                return null;
            }

            if (variable.IsByReference)
            {
                // error??
                Error("GetReadFunc variable.IsByReference error.");
                return null;
            }

            MethodDefinition newReaderFunc;

            if (variable.IsArray)
            {
                TypeReference elementType = variable.GetElementType();
                MethodReference elementReadFunc = GetReadFunc(elementType);
                if (elementReadFunc == null)
                {
                    return null;
                }
                newReaderFunc = GenerateArrayReadFunc(variable, elementReadFunc);
            }
            else
            {
                if (td.IsEnum)
                {
                    return NetworkReaderReadInt32;
                }

                newReaderFunc = GenerateReadFunction(variable);
            }

            if (newReaderFunc == null)
            {
                Log.Error("GetReadFunc unable to generate function for:" + variable.FullName);
                return null;
            }
            RegisterReadFunc(variable.FullName, newReaderFunc);
            return newReaderFunc;
        }

        public static void RegisterReadFunc(string name, MethodDefinition newReaderFunc)
        {
            WeaveLists.readFuncs[name] = newReaderFunc;
            WeaveLists.generatedReadFunctions.Add(newReaderFunc);

            ConfirmGeneratedCodeClass(CurrentAssembly.MainModule);
            WeaveLists.generateContainerClass.Methods.Add(newReaderFunc);
        }

        static MethodDefinition GenerateArrayReadFunc(TypeReference variable, MethodReference elementReadFunc)
        {
            if (!variable.IsArrayType())
            {
                Log.Error(variable.FullName + " is an unsupported array type. Jagged and multidimensional arrays are not supported");
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
            MethodDefinition readerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    variable);

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, CurrentAssembly.MainModule.ImportReference(NetworkReaderType)));

            readerFunc.Body.Variables.Add(new VariableDefinition(int32Type));
            readerFunc.Body.Variables.Add(new VariableDefinition(variable));
            readerFunc.Body.Variables.Add(new VariableDefinition(int32Type));
            readerFunc.Body.InitLocals = true;

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Call, NetworkReadUInt16));
            worker.Append(worker.Create(OpCodes.Stloc_0));
            worker.Append(worker.Create(OpCodes.Ldloc_0));

            Instruction labelEmptyArray = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Brtrue, labelEmptyArray));

            // return empty array
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Newarr, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Ret));

            // create the actual array
            worker.Append(labelEmptyArray);
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Newarr, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Stloc_1));
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Stloc_2));

            // loop start
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Br, labelHead));

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);
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

            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        static MethodDefinition GenerateArrayWriteFunc(TypeReference variable, MethodReference elementWriteFunc)
        {
            if (!variable.IsArrayType())
            {
                Log.Error(variable.FullName + " is an unsupported array type. Jagged and multidimensional arrays are not supported");
                return null;
            }
            string functionName = "_WriteArray" + variable.GetElementType().Name + "_";
            if (variable.DeclaringType != null)
            {
                functionName += variable.DeclaringType.Name;
            }
            else
            {
                functionName += "None";
            }

            // create new writer for this type
            MethodDefinition writerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    voidType);

            writerFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, CurrentAssembly.MainModule.ImportReference(NetworkWriterType)));
            writerFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, CurrentAssembly.MainModule.ImportReference(variable)));

            writerFunc.Body.Variables.Add(new VariableDefinition(uint16Type));
            writerFunc.Body.Variables.Add(new VariableDefinition(uint16Type));
            writerFunc.Body.InitLocals = true;

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            // null check
            Instruction labelNull = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Brtrue, labelNull));

            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Call, NetworkWriteUInt16));
            worker.Append(worker.Create(OpCodes.Ret));

            // setup array length local variable
            worker.Append(labelNull);
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Ldlen));
            worker.Append(worker.Create(OpCodes.Conv_I4));
            worker.Append(worker.Create(OpCodes.Conv_U2));
            worker.Append(worker.Create(OpCodes.Stloc_0));

            //write length
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Call, NetworkWriteUInt16));

            // start loop
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Stloc_1));
            Instruction labelHead = worker.Create(OpCodes.Nop);
            worker.Append(worker.Create(OpCodes.Br, labelHead));

            // loop body
            Instruction labelBody = worker.Create(OpCodes.Nop);
            worker.Append(labelBody);
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldelema, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Ldobj, variable.GetElementType()));
            worker.Append(worker.Create(OpCodes.Call, elementWriteFunc));
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldc_I4_1));
            worker.Append(worker.Create(OpCodes.Add));
            worker.Append(worker.Create(OpCodes.Conv_U2));
            worker.Append(worker.Create(OpCodes.Stloc_1));

            // loop while check
            worker.Append(labelHead);
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Ldlen));
            worker.Append(worker.Create(OpCodes.Conv_I4));
            worker.Append(worker.Create(OpCodes.Blt, labelBody));

            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }

        static MethodDefinition GenerateWriterFunction(TypeReference variable)
        {
            if (!IsValidTypeToGenerate(variable.Resolve()))
            {
                return null;
            }

            string functionName = "_Write" + variable.Name + "_";
            if (variable.DeclaringType != null)
            {
                functionName += variable.DeclaringType.Name;
            }
            else
            {
                functionName += "None";
            }
            // create new writer for this type
            MethodDefinition writerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    voidType);

            writerFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, CurrentAssembly.MainModule.ImportReference(NetworkWriterType)));
            writerFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, CurrentAssembly.MainModule.ImportReference(variable)));

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            uint fields = 0;
            foreach (FieldDefinition field in variable.Resolve().Fields)
            {
                if (field.IsStatic || field.IsPrivate)
                    continue;

                if (field.FieldType.Resolve().HasGenericParameters)
                {
                    Weaver.Error("WriteReadFunc for " + field.Name + " [" + field.FieldType + "/" + field.FieldType.FullName + "]. Cannot have generic parameters.");
                    return null;
                }

                if (field.FieldType.Resolve().IsInterface)
                {
                    Weaver.Error("WriteReadFunc for " + field.Name + " [" + field.FieldType + "/" + field.FieldType.FullName + "]. Cannot be an interface.");
                    return null;
                }

                MethodReference writeFunc = GetWriteFunc(field.FieldType);
                if (writeFunc != null)
                {
                    fields++;
                    worker.Append(worker.Create(OpCodes.Ldarg_0));
                    worker.Append(worker.Create(OpCodes.Ldarg_1));
                    worker.Append(worker.Create(OpCodes.Ldfld, field));
                    worker.Append(worker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error("WriteReadFunc for " + field.Name + " type " + field.FieldType + " no supported");
                    return null;
                }
            }
            if (fields == 0)
            {
                Log.Warning("The class / struct " + variable.Name + " has no public or non-static fields to serialize");
            }
            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }

        static MethodDefinition GenerateReadFunction(TypeReference variable)
        {
            if (RecursionCount++ > MaxRecursionCount)
            {
                Weaver.Error("GetReadFunc recursion depth exceeded for " + variable.Name + ". Check for self-referencing member variables.");
                return null;
            }

            if (!IsValidTypeToGenerate(variable.Resolve()))
            {
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
            MethodDefinition readerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    variable);

            // create local for return value
            readerFunc.Body.Variables.Add(new VariableDefinition(variable));
            readerFunc.Body.InitLocals = true;

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, CurrentAssembly.MainModule.ImportReference(NetworkReaderType)));

            ILProcessor worker = readerFunc.Body.GetILProcessor();

            if (variable.IsValueType)
            {
                // structs are created with Initobj
                worker.Append(worker.Create(OpCodes.Ldloca, 0));
                worker.Append(worker.Create(OpCodes.Initobj, variable));
            }
            else
            {
                // classes are created with their constructor

                MethodDefinition ctor = Resolvers.ResolveDefaultPublicCtor(variable);
                if (ctor == null)
                {
                    Log.Error("The class " + variable.Name + " has no default constructor or it's private, aborting.");
                    return null;
                }

                worker.Append(worker.Create(OpCodes.Newobj, ctor));
                worker.Append(worker.Create(OpCodes.Stloc_0));
            }

            uint fields = 0;
            foreach (FieldDefinition field in variable.Resolve().Fields)
            {
                if (field.IsStatic || field.IsPrivate)
                    continue;

                // mismatched ldloca/ldloc for struct/class combinations is invalid IL, which causes crash at runtime
                OpCode opcode = variable.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc;
                worker.Append(worker.Create(opcode, 0));

                MethodReference readFunc = GetReadFunc(field.FieldType);
                if (readFunc != null)
                {
                    worker.Append(worker.Create(OpCodes.Ldarg_0));
                    worker.Append(worker.Create(OpCodes.Call, readFunc));
                }
                else
                {
                    Weaver.Error("GetReadFunc for " + field.Name + " type " + field.FieldType + " no supported");
                    return null;
                }

                worker.Append(worker.Create(OpCodes.Stfld, field));
                fields++;
            }
            if (fields == 0)
            {
                Log.Warning("The class / struct " + variable.Name + " has no public or non-static fields to serialize");
            }

            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ret));
            return readerFunc;
        }

        static void ConfirmGeneratedCodeClass(ModuleDefinition moduleDef)
        {
            if (WeaveLists.generateContainerClass == null)
            {
                WeaveLists.generateContainerClass = new TypeDefinition("Mirror", "GeneratedNetworkCode",
                        TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.Public | TypeAttributes.AutoClass,
                        objectType);

                const MethodAttributes methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                MethodDefinition method = new MethodDefinition(".ctor", methodAttributes, voidType);
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, Resolvers.ResolveMethod(objectType, CurrentAssembly, ".ctor")));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

                WeaveLists.generateContainerClass.Methods.Add(method);
            }
        }


        static void ProcessPropertySites()
        {
            PropertySiteProcessor.ProcessSitesModule(CurrentAssembly.MainModule);
        }

        static bool ProcessNetworkBehaviourType(TypeDefinition td)
        {
            if (!NetworkBehaviourProcessor.WasProcessed(td))
            {
                DLog(td, "Found NetworkBehaviour " + td.FullName);

                NetworkBehaviourProcessor proc = new NetworkBehaviourProcessor(td);
                proc.Process();
                return true;
            }
            return false;
        }

        static void SetupUnityTypes()
        {
            vector2Type = UnityAssembly.MainModule.GetType("UnityEngine.Vector2");
            vector3Type = UnityAssembly.MainModule.GetType("UnityEngine.Vector3");
            vector4Type = UnityAssembly.MainModule.GetType("UnityEngine.Vector4");
            vector2IntType = UnityAssembly.MainModule.GetType("UnityEngine.Vector2Int");
            vector3IntType = UnityAssembly.MainModule.GetType("UnityEngine.Vector3Int");
            colorType = UnityAssembly.MainModule.GetType("UnityEngine.Color");
            color32Type = UnityAssembly.MainModule.GetType("UnityEngine.Color32");
            quaternionType = UnityAssembly.MainModule.GetType("UnityEngine.Quaternion");
            rectType = UnityAssembly.MainModule.GetType("UnityEngine.Rect");
            planeType = UnityAssembly.MainModule.GetType("UnityEngine.Plane");
            rayType = UnityAssembly.MainModule.GetType("UnityEngine.Ray");
            matrixType = UnityAssembly.MainModule.GetType("UnityEngine.Matrix4x4");
            gameObjectType = UnityAssembly.MainModule.GetType("UnityEngine.GameObject");
            transformType = UnityAssembly.MainModule.GetType("UnityEngine.Transform");
            unityObjectType = UnityAssembly.MainModule.GetType("UnityEngine.Object");

            NetworkClientType = NetAssembly.MainModule.GetType("Mirror.NetworkClient");
            NetworkServerType = NetAssembly.MainModule.GetType("Mirror.NetworkServer");

            SyncVarType = NetAssembly.MainModule.GetType("Mirror.SyncVarAttribute");
            CommandType = NetAssembly.MainModule.GetType("Mirror.CommandAttribute");
            ClientRpcType = NetAssembly.MainModule.GetType("Mirror.ClientRpcAttribute");
            TargetRpcType = NetAssembly.MainModule.GetType("Mirror.TargetRpcAttribute");
            SyncEventType = NetAssembly.MainModule.GetType("Mirror.SyncEventAttribute");
            SyncObjectType = NetAssembly.MainModule.GetType("Mirror.SyncObject");
        }

        static void SetupCorLib()
        {
            AssemblyNameReference name = AssemblyNameReference.Parse("mscorlib");
            ReaderParameters parameters = new ReaderParameters
            {
                AssemblyResolver = CurrentAssembly.MainModule.AssemblyResolver,
            };
            CorLibModule = CurrentAssembly.MainModule.AssemblyResolver.Resolve(name, parameters).MainModule;
        }

        static TypeReference ImportCorLibType(string fullName)
        {
            TypeDefinition type = CorLibModule.GetType(fullName) ?? CorLibModule.ExportedTypes.First(t => t.FullName == fullName).Resolve();
            return CurrentAssembly.MainModule.ImportReference(type);
        }

        static void SetupTargetTypes()
        {
            // system types
            SetupCorLib();
            voidType = ImportCorLibType("System.Void");
            singleType = ImportCorLibType("System.Single");
            doubleType = ImportCorLibType("System.Double");
            decimalType = ImportCorLibType("System.Decimal");
            boolType = ImportCorLibType("System.Boolean");
            stringType = ImportCorLibType("System.String");
            int64Type = ImportCorLibType("System.Int64");
            uint64Type = ImportCorLibType("System.UInt64");
            int32Type = ImportCorLibType("System.Int32");
            uint32Type = ImportCorLibType("System.UInt32");
            int16Type = ImportCorLibType("System.Int16");
            uint16Type = ImportCorLibType("System.UInt16");
            byteType = ImportCorLibType("System.Byte");
            sbyteType = ImportCorLibType("System.SByte");
            charType = ImportCorLibType("System.Char");
            objectType = ImportCorLibType("System.Object");
            valueTypeType = ImportCorLibType("System.ValueType");
            typeType = ImportCorLibType("System.Type");
            IEnumeratorType = ImportCorLibType("System.Collections.IEnumerator");
            guidType = ImportCorLibType("System.Guid");

            NetworkReaderType = NetAssembly.MainModule.GetType("Mirror.NetworkReader");
            NetworkReaderDef = NetworkReaderType.Resolve();

            NetworkReaderCtor = Resolvers.ResolveMethod(NetworkReaderDef, CurrentAssembly, ".ctor");

            NetworkWriterType = NetAssembly.MainModule.GetType("Mirror.NetworkWriter");
            NetworkWriterDef  = NetworkWriterType.Resolve();

            NetworkWriterCtor = Resolvers.ResolveMethod(NetworkWriterDef, CurrentAssembly, ".ctor");

            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, CurrentAssembly, "get_active");
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, CurrentAssembly, "get_localClientActive");
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, CurrentAssembly, "get_active");

            NetworkReaderReadInt32 = Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadInt32");

            NetworkWriterWriteInt32 = Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", int32Type);
            NetworkWriterWriteInt16 = Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", int16Type);

            NetworkReaderReadPackedUInt32 = Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadPackedUInt32");
            NetworkReaderReadPackedInt32 = Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadPackedInt32");
            NetworkReaderReadPackedUInt64 = Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadPackedUInt64");
            NetworkReaderReadPackedInt64 = Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadPackedInt64");
            NetworkReaderReadByte = Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadByte");

            NetworkWriterWritePackedUInt32 = Resolvers.ResolveMethod(NetworkWriterType, CurrentAssembly, "WritePackedUInt32");
            NetworkWriterWritePackedInt32 = Resolvers.ResolveMethod(NetworkWriterType, CurrentAssembly, "WritePackedInt32");
            NetworkWriterWritePackedUInt64 = Resolvers.ResolveMethod(NetworkWriterType, CurrentAssembly, "WritePackedUInt64");
            NetworkWriterWritePackedInt64 = Resolvers.ResolveMethod(NetworkWriterType, CurrentAssembly, "WritePackedInt64");

            NetworkReadUInt16 = Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadUInt16");
            NetworkWriteUInt16 = Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", uint16Type);

            CmdDelegateReference = NetAssembly.MainModule.GetType("Mirror.NetworkBehaviour/CmdDelegate");
            CmdDelegateConstructor = Resolvers.ResolveMethod(CmdDelegateReference, CurrentAssembly, ".ctor");
            CurrentAssembly.MainModule.ImportReference(gameObjectType);
            CurrentAssembly.MainModule.ImportReference(transformType);

            TypeReference netViewTmp = NetAssembly.MainModule.GetType("Mirror.NetworkIdentity");
            NetworkIdentityType = CurrentAssembly.MainModule.ImportReference(netViewTmp);

            NetworkBehaviourType = NetAssembly.MainModule.GetType("Mirror.NetworkBehaviour");
            NetworkBehaviourType2 = CurrentAssembly.MainModule.ImportReference(NetworkBehaviourType);
            NetworkConnectionType = NetAssembly.MainModule.GetType("Mirror.NetworkConnection");

            MonoBehaviourType = UnityAssembly.MainModule.GetType("UnityEngine.MonoBehaviour");
            ScriptableObjectType = UnityAssembly.MainModule.GetType("UnityEngine.ScriptableObject");

            NetworkConnectionType = NetAssembly.MainModule.GetType("Mirror.NetworkConnection");
            NetworkConnectionType = CurrentAssembly.MainModule.ImportReference(NetworkConnectionType);

            MessageBaseType = NetAssembly.MainModule.GetType("Mirror.MessageBase");
            SyncListType = NetAssembly.MainModule.GetType("Mirror.SyncList`1");
            SyncSetType = NetAssembly.MainModule.GetType("Mirror.SyncSet`1");
            SyncDictionaryType = NetAssembly.MainModule.GetType("Mirror.SyncDictionary`2");

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveProperty(NetworkBehaviourType, CurrentAssembly, "syncVarDirtyBits");

            ComponentType = UnityAssembly.MainModule.GetType("UnityEngine.Component");
            ClientSceneType = NetAssembly.MainModule.GetType("Mirror.ClientScene");
            ReadyConnectionReference = Resolvers.ResolveMethod(ClientSceneType, CurrentAssembly, "get_readyConnection");

            // get specialized GetComponent<NetworkIdentity>()
            getComponentReference = Resolvers.ResolveMethodGeneric(ComponentType, CurrentAssembly, "GetComponent", NetworkIdentityType);

            getNetIdReference = Resolvers.ResolveMethod(netViewTmp, CurrentAssembly, "get_netId");

            gameObjectInequality = Resolvers.ResolveMethod(unityObjectType, CurrentAssembly, "op_Inequality");

            UBehaviourIsServer  = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "get_isServer");
            setSyncVarReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SetSyncVar");
            setSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "set_syncVarHookGuard");
            getSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "get_syncVarHookGuard");

            setSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SetSyncVarGameObject");
            getSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "GetSyncVarGameObject");
            setSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SetSyncVarNetworkIdentity");
            getSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "GetSyncVarNetworkIdentity");
            registerCommandDelegateReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "RegisterCommandDelegate");
            registerRpcDelegateReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "RegisterRpcDelegate");
            registerEventDelegateReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "RegisterEventDelegate");
            getTypeReference = Resolvers.ResolveMethod(objectType, CurrentAssembly, "GetType");
            getTypeFromHandleReference = Resolvers.ResolveMethod(typeType, CurrentAssembly, "GetTypeFromHandle");
            logErrorReference = Resolvers.ResolveMethod(UnityAssembly.MainModule.GetType("UnityEngine.Debug"), CurrentAssembly, "LogError");
            logWarningReference = Resolvers.ResolveMethod(UnityAssembly.MainModule.GetType("UnityEngine.Debug"), CurrentAssembly, "LogWarning");
            sendCommandInternal = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SendCommandInternal");
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SendRPCInternal");
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SendTargetRPCInternal");
            sendEventInternal = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SendEventInternal");

            SyncObjectType = CurrentAssembly.MainModule.ImportReference(SyncObjectType);
            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "InitSyncObject");
        }

        static void SetupReadFunctions()
        {
            WeaveLists.readFuncs = new Dictionary<string, MethodReference>
            {
                { singleType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadSingle") },
                { doubleType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadDouble") },
                { boolType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadBoolean") },
                { stringType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadString") },
                { int64Type.FullName, NetworkReaderReadPackedInt64 },
                { uint64Type.FullName, NetworkReaderReadPackedUInt64 },
                { int32Type.FullName, NetworkReaderReadPackedInt32 },
                { uint32Type.FullName, NetworkReaderReadPackedUInt32 },
                { int16Type.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadInt16") },
                { uint16Type.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadUInt16") },
                { byteType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadByte") },
                { sbyteType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadSByte") },
                { charType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadChar") },
                { decimalType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadDecimal") },
                { vector2Type.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadVector2") },
                { vector3Type.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadVector3") },
                { vector4Type.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadVector4") },
                { vector2IntType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadVector2Int") },
                { vector3IntType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadVector3Int") },
                { colorType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadColor") },
                { color32Type.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadColor32") },
                { quaternionType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadQuaternion") },
                { rectType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadRect") },
                { planeType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadPlane") },
                { rayType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadRay") },
                { matrixType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadMatrix4x4") },
                { guidType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadGuid") },
                { gameObjectType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadGameObject") },
                { NetworkIdentityType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadNetworkIdentity") },
                { transformType.FullName, Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadTransform") },
                { "System.Byte[]", Resolvers.ResolveMethod(NetworkReaderType, CurrentAssembly, "ReadBytesAndSize") },
            };
        }

        static void SetupWriteFunctions()
        {
            WeaveLists.writeFuncs = new Dictionary<string, MethodReference>
            {
                { singleType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", singleType) },
                { doubleType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", doubleType) },
                { boolType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", boolType) },
                { stringType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", stringType) },
                { int64Type.FullName, NetworkWriterWritePackedInt64 },
                { uint64Type.FullName, NetworkWriterWritePackedUInt64 },
                { int32Type.FullName, NetworkWriterWritePackedInt32 },
                { uint32Type.FullName, NetworkWriterWritePackedUInt32 },
                { int16Type.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", int16Type) },
                { uint16Type.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", uint16Type) },
                { byteType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", byteType) },
                { sbyteType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", sbyteType) },
                { charType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", charType) },
                { decimalType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", decimalType) },
                { vector2Type.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", vector2Type) },
                { vector3Type.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", vector3Type) },
                { vector4Type.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", vector4Type) },
                { vector2IntType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", vector2IntType) },
                { vector3IntType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", vector3IntType) },
                { colorType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", colorType) },
                { color32Type.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", color32Type) },
                { quaternionType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", quaternionType) },
                { rectType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", rectType) },
                { planeType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", planeType) },
                { rayType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", rayType) },
                { matrixType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", matrixType) },
                { guidType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", guidType) },
                { gameObjectType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", gameObjectType) },
                { NetworkIdentityType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", NetworkIdentityType) },
                { transformType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "Write", transformType) },
                { "System.Byte[]", Resolvers.ResolveMethodWithArg(NetworkWriterType, CurrentAssembly, "WriteBytesAndSize", "System.Byte[]") }
            };
        }

        public static bool IsNetworkBehaviour(TypeDefinition td)
        {
            return td.IsDerivedFrom(NetworkBehaviourType);
        }

        public static bool IsValidTypeToGenerate(TypeDefinition variable)
        {
            // a valid type is a simple class or struct. so we generate only code for types we dont know, and if they are not inside
            // this assembly it must mean that we are trying to serialize a variable outside our scope. and this will fail.

            string assembly = CurrentAssembly.MainModule.Name;
            if (variable.Module.Name != assembly)
            {
                Weaver.Error("parameter [" + variable.Name +
                    "] is of the type [" +
                    variable.FullName +
                    "] is not a valid type, please make sure to use a valid type.");
                return false;
            }
            return true;
        }

        static void CheckMonoBehaviour(TypeDefinition td)
        {
            if (td.IsDerivedFrom(MonoBehaviourType))
            {
                MonoBehaviourProcessor.Process(td);
            }
        }

        static bool CheckNetworkBehaviour(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            if (!IsNetworkBehaviour(td))
            {
                CheckMonoBehaviour(td);
                return false;
            }

            // process this and base classes from parent to child order

            List<TypeDefinition> behaviourClasses = new List<TypeDefinition>();

            TypeDefinition parent = td;
            while (parent != null)
            {
                if (parent.FullName == NetworkBehaviourType.FullName)
                {
                    break;
                }
                try
                {
                    behaviourClasses.Insert(0, parent);
                    parent = parent.BaseType.Resolve();
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for plugins.
                    //Console.WriteLine("AssemblyResolutionException: "+ ex.ToString());
                    break;
                }
            }

            bool didWork = false;
            foreach (TypeDefinition behaviour in behaviourClasses)
            {
                didWork |= ProcessNetworkBehaviourType(behaviour);
            }
            return didWork;
        }

        static bool CheckMessageBase(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            bool didWork = false;

            // are ANY parent classes MessageBase
            TypeReference parent = td.BaseType;
            while (parent != null)
            {
                if (parent.FullName == MessageBaseType.FullName)
                {
                    MessageClassProcessor.Process(td);
                    didWork = true;
                    break;
                }
                try
                {
                    parent = parent.Resolve().BaseType;
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for plugins.
                    //Console.WriteLine("AssemblyResolutionException: "+ ex.ToString());
                    break;
                }
            }

            // check for embedded types
            foreach (TypeDefinition embedded in td.NestedTypes)
            {
                didWork |= CheckMessageBase(embedded);
            }

            return didWork;
        }

        static bool CheckSyncList(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            bool didWork = false;

            // are ANY parent classes SyncListStruct
            TypeReference parent = td.BaseType;
            while (parent != null)
            {
                if (parent.FullName.StartsWith(SyncListType.FullName))
                {
                    SyncListProcessor.Process(td);
                    didWork = true;
                    break;
                }
                else if (parent.FullName.StartsWith(SyncSetType.FullName))
                {
                    SyncListProcessor.Process(td);
                    didWork = true;
                    break;
                }
                else if (parent.FullName.StartsWith(SyncDictionaryType.FullName))
                {
                    SyncDictionaryProcessor.Process(td);
                    didWork = true;
                    break;
                }
                try
                {
                    parent = parent.Resolve().BaseType;
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for pluins.
                    //Console.WriteLine("AssemblyResolutionException: "+ ex.ToString());
                    break;
                }
            }

            // check for embedded types
            foreach (TypeDefinition embedded in td.NestedTypes)
            {
                didWork |= CheckSyncList(embedded);
            }

            return didWork;
        }

        static bool Weave(string assName, IEnumerable<string> dependencies, IAssemblyResolver assemblyResolver, string unityEngineDLLPath, string mirrorNetDLLPath, string outputDir)
        {
            ReaderParameters readParams = Helpers.ReaderParameters(assName, dependencies, assemblyResolver, unityEngineDLLPath, mirrorNetDLLPath);

            using (CurrentAssembly = AssemblyDefinition.ReadAssembly(assName, readParams))
            {
                SetupTargetTypes();
                SetupReadFunctions();
                SetupWriteFunctions();

                ModuleDefinition moduleDefinition = CurrentAssembly.MainModule;
                Console.WriteLine("Script Module: {0}", moduleDefinition.Name);

                // Process each NetworkBehaviour
                bool didWork = false;

                // We need to do 2 passes, because SyncListStructs might be referenced from other modules, so we must make sure we generate them first.
                for (int pass = 0; pass < 2; pass++)
                {
                    System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
                    foreach (TypeDefinition td in moduleDefinition.Types)
                    {
                        if (td.IsClass && td.BaseType.CanBeResolved())
                        {
                            try
                            {
                                if (pass == 0)
                                {
                                    didWork |= CheckSyncList(td);
                                }
                                else
                                {
                                    didWork |= CheckNetworkBehaviour(td);
                                    didWork |= CheckMessageBase(td);
                                }
                            }
                            catch (Exception ex)
                            {
                                if (CurrentAssembly.MainModule.SymbolReader != null)
                                    CurrentAssembly.MainModule.SymbolReader.Dispose();
                                Weaver.Error(ex.Message);
                                throw ex;
                            }
                        }

                        if (WeavingFailed)
                        {
                            if (CurrentAssembly.MainModule.SymbolReader != null)
                                CurrentAssembly.MainModule.SymbolReader.Dispose();
                            return false;
                        }
                    }
                    watch.Stop();
                    Console.WriteLine("Pass: " + pass + " took " + watch.ElapsedMilliseconds + " milliseconds");
                }

                if (didWork)
                {
                    // this must be done for ALL code, not just NetworkBehaviours
                    try
                    {
                        ProcessPropertySites();
                    }
                    catch (Exception e)
                    {
                        Log.Error("ProcessPropertySites exception: " + e);
                        if (CurrentAssembly.MainModule.SymbolReader != null)
                            CurrentAssembly.MainModule.SymbolReader.Dispose();
                        return false;
                    }

                    if (WeavingFailed)
                    {
                        //Log.Error("Failed phase II.");
                        if (CurrentAssembly.MainModule.SymbolReader != null)
                            CurrentAssembly.MainModule.SymbolReader.Dispose();
                        return false;
                    }

                    string dest = Helpers.DestinationFileFor(outputDir, assName);
                    //Console.WriteLine ("Output:" + dest);

                    WriterParameters writeParams = Helpers.GetWriterParameters(readParams);
                    CurrentAssembly.Write(dest, writeParams);
                }

                if (CurrentAssembly.MainModule.SymbolReader != null)
                    CurrentAssembly.MainModule.SymbolReader.Dispose();
            }

            return true;
        }

        public static bool WeaveAssemblies(IEnumerable<string> assemblies, IEnumerable<string> dependencies, IAssemblyResolver assemblyResolver, string outputDir, string unityEngineDLLPath, string mirrorNetDLLPath)
        {
            WeavingFailed = false;
            WeaveLists = new WeaverLists();

            UnityAssembly = AssemblyDefinition.ReadAssembly(unityEngineDLLPath);
            NetAssembly = AssemblyDefinition.ReadAssembly(mirrorNetDLLPath);

            SetupUnityTypes();

            try
            {
                foreach (string ass in assemblies)
                {
                    if (!Weave(ass, dependencies, assemblyResolver, unityEngineDLLPath, mirrorNetDLLPath, outputDir))
                    {
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Exception :" + e);
                return false;
            }
            CorLibModule = null;
            return true;
        }
    }
}
