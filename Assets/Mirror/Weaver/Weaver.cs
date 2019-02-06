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
        // UNetwork types
        public static TypeReference NetworkBehaviourType;
        public static TypeReference NetworkBehaviourType2;
        public static TypeReference MonoBehaviourType;
        public static TypeReference ScriptableObjectType;
        public static TypeReference NetworkConnectionType;
        public static TypeReference ULocalConnectionToServerType;
        public static TypeReference ULocalConnectionToClientType;

        public static TypeReference MessageBaseType;
        public static TypeReference SyncListStructType;

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
        public static MethodReference getUNetIdReference;
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
        public static MethodReference NetworkReaderReadPacked32;
        public static MethodReference NetworkReaderReadPacked64;
        public static MethodReference NetworkReaderReadByte;
        public static MethodReference NetworkWriterWritePacked32;
        public static MethodReference NetworkWriterWritePacked64;

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

        public static WeaverLists lists;

        public static AssemblyDefinition scriptDef;
        public static ModuleDefinition corLib;
        public static AssemblyDefinition m_UnityAssemblyDefinition;
        public static AssemblyDefinition m_UNetAssemblyDefinition;

        static bool m_DebugFlag = true;

        public static bool fail;
        public static bool generateLogErrors = false;

        // this is used to prevent stack overflows when generating serialization code when there are self-referencing types.
        // All the utility classes use GetWriteFunc() to generate serialization code, so the recursion check is implemented there instead of in each utility class.
        // A NetworkBehaviour with the max SyncVars (32) can legitimately increment this value to 65 - so max must be higher than that
        const int MaxRecursionCount = 128;
        static int s_RecursionCount;
        public static void ResetRecursionCount()
        {
            s_RecursionCount = 0;
        }

        public static void DLog(TypeDefinition td, string fmt, params object[] args)
        {
            if (!m_DebugFlag)
                return;

            Console.WriteLine("[" + td.Name + "] " + String.Format(fmt, args));
        }

        public static int GetSyncVarStart(string className)
        {
            return lists.numSyncVars.ContainsKey(className)
                   ? lists.numSyncVars[className]
                   : 0;
        }

        public static void SetNumSyncVars(string className, int num)
        {
            lists.numSyncVars[className] = num;
        }

        public static MethodReference GetWriteFunc(TypeReference variable)
        {
            if (s_RecursionCount++ > MaxRecursionCount)
            {
                Log.Error("GetWriteFunc recursion depth exceeded for " + variable.Name + ". Check for self-referencing member variables.");
                fail = true;
                return null;
            }

            if (lists.writeFuncs.ContainsKey(variable.FullName))
            {
                MethodReference foundFunc = lists.writeFuncs[variable.FullName];
                if (foundFunc.Parameters[0].ParameterType.IsArray == variable.IsArray)
                {
                    return foundFunc;
                }
            }

            if (variable.IsByReference)
            {
                // error??
                Log.Error("GetWriteFunc variable.IsByReference error.");
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
            lists.writeFuncs[name] = newWriterFunc;
            lists.generatedWriteFunctions.Add(newWriterFunc);

            ConfirmGeneratedCodeClass(scriptDef.MainModule);
            lists.generateContainerClass.Methods.Add(newWriterFunc);
        }

        public static MethodReference GetReadFunc(TypeReference variable)
        {
            if (lists.readFuncs.ContainsKey(variable.FullName))
            {
                MethodReference foundFunc = lists.readFuncs[variable.FullName];
                if (foundFunc.ReturnType.IsArray == variable.IsArray)
                {
                    return foundFunc;
                }
            }

            TypeDefinition td = variable.Resolve();
            if (td == null)
            {
                Log.Error("GetReadFunc unsupported type " + variable.FullName);
                return null;
            }

            if (variable.IsByReference)
            {
                // error??
                Log.Error("GetReadFunc variable.IsByReference error.");
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
            lists.readFuncs[name] = newReaderFunc;
            lists.generatedReadFunctions.Add(newReaderFunc);

            ConfirmGeneratedCodeClass(scriptDef.MainModule);
            lists.generateContainerClass.Methods.Add(newReaderFunc);
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

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, scriptDef.MainModule.ImportReference(NetworkReaderType)));

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

            writerFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, scriptDef.MainModule.ImportReference(NetworkWriterType)));
            writerFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, scriptDef.MainModule.ImportReference(variable)));

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

            writerFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, scriptDef.MainModule.ImportReference(NetworkWriterType)));
            writerFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, scriptDef.MainModule.ImportReference(variable)));

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            uint fields = 0;
            foreach (FieldDefinition field in variable.Resolve().Fields)
            {
                if (field.IsStatic || field.IsPrivate)
                    continue;

                if (field.FieldType.Resolve().HasGenericParameters)
                {
                    Weaver.fail = true;
                    Log.Error("WriteReadFunc for " + field.Name + " [" + field.FieldType + "/" + field.FieldType.FullName + "]. Cannot have generic parameters.");
                    return null;
                }

                if (field.FieldType.Resolve().IsInterface)
                {
                    Weaver.fail = true;
                    Log.Error("WriteReadFunc for " + field.Name + " [" + field.FieldType + "/" + field.FieldType.FullName + "]. Cannot be an interface.");
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
                    Log.Error("WriteReadFunc for " + field.Name + " type " + field.FieldType + " no supported");
                    fail = true;
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
            if (s_RecursionCount++ > MaxRecursionCount)
            {
                Log.Error("GetReadFunc recursion depth exceeded for " + variable.Name + ". Check for self-referencing member variables.");
                fail = true;
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

            readerFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, scriptDef.MainModule.ImportReference(NetworkReaderType)));

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
                    Log.Error("GetReadFunc for " + field.Name + " type " + field.FieldType + " no supported");
                    fail = true;
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

        static void ProcessInstructionMethod(ModuleDefinition moduleDef, TypeDefinition td, MethodDefinition md, Instruction instr, MethodReference opMethodRef, int iCount)
        {
            //DLog(td, "ProcessInstructionMethod " + opMethod.Name);
            if (opMethodRef.Name == "Invoke")
            {
                // Events use an "Invoke" method to call the delegate.
                // this code replaces the "Invoke" instruction with the generated "Call***" instruction which send the event to the server.
                // but the "Invoke" instruction is called on the event field - where the "call" instruction is not.
                // so the earlier instruction that loads the event field is replaced with a Noop.

                // go backwards until find a ldfld instruction that matches ANY event
                bool found = false;
                while (iCount > 0 && !found)
                {
                    iCount -= 1;
                    Instruction inst = md.Body.Instructions[iCount];
                    if (inst.OpCode == OpCodes.Ldfld)
                    {
                        FieldReference opField = inst.Operand as FieldReference;

                        // find replaceEvent with matching name
                        // NOTE: original weaver compared .Name, not just the MethodDefinition,
                        //       that's why we use dict<string,method>.
                        // TODO maybe replaceEvents[md] would work too?
                        MethodDefinition replacement;
                        if (lists.replaceEvents.TryGetValue(opField.Name, out replacement))
                        {
                            instr.Operand = replacement;
                            inst.OpCode = OpCodes.Nop;
                            found = true;
                        }
                    }
                }
            }
            else
            {
                // should it be replaced?
                // NOTE: original weaver compared .FullName, not just the MethodDefinition,
                //       that's why we use dict<string,method>.
                // TODO maybe replaceMethods[md] would work too?
                MethodDefinition replacement;
                if (lists.replaceMethods.TryGetValue(opMethodRef.FullName, out replacement))
                {
                    //DLog(td, "    replacing "  + md.Name + ":" + i);
                    instr.Operand = replacement;
                    //DLog(td, "    replaced  "  + md.Name + ":" + i);
                }
            }
        }

        static void ConfirmGeneratedCodeClass(ModuleDefinition moduleDef)
        {
            if (lists.generateContainerClass == null)
            {
                lists.generateContainerClass = new TypeDefinition("Mirror", "GeneratedNetworkCode",
                        TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.Public | TypeAttributes.AutoClass,
                        objectType);

                const MethodAttributes methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                MethodDefinition method = new MethodDefinition(".ctor", methodAttributes, voidType);
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, Resolvers.ResolveMethod(objectType, scriptDef, ".ctor")));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

                lists.generateContainerClass.Methods.Add(method);
            }
        }

        // replaces syncvar write access with the NetworkXYZ.get property calls
        static void ProcessInstructionSetterField(TypeDefinition td, MethodDefinition md, Instruction i, FieldDefinition opField)
        {
            // dont replace property call sites in constructors
            if (md.Name == ".ctor")
                return;

            // does it set a field that we replaced?
            MethodDefinition replacement;
            if (lists.replacementSetterProperties.TryGetValue(opField, out replacement))
            {
                //replace with property
                //DLog(td, "    replacing "  + md.Name + ":" + i);
                i.OpCode = OpCodes.Call;
                i.Operand = replacement;
                //DLog(td, "    replaced  "  + md.Name + ":" + i);
            }
        }

        // replaces syncvar read access with the NetworkXYZ.get property calls
        static void ProcessInstructionGetterField(TypeDefinition td, MethodDefinition md, Instruction i, FieldDefinition opField)
        {
            // dont replace property call sites in constructors
            if (md.Name == ".ctor")
                return;

            // does it set a field that we replaced?
            MethodDefinition replacement;
            if (lists.replacementGetterProperties.TryGetValue(opField, out replacement))
            {
                //replace with property
                //DLog(td, "    replacing "  + md.Name + ":" + i);
                i.OpCode = OpCodes.Call;
                i.Operand = replacement;
                //DLog(td, "    replaced  "  + md.Name + ":" + i);
            }
        }

        static void ProcessInstruction(ModuleDefinition moduleDef, TypeDefinition td, MethodDefinition md, Instruction i, int iCount)
        {
            if (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt)
            {
                MethodReference opMethod = i.Operand as MethodReference;
                if (opMethod != null)
                {
                    ProcessInstructionMethod(moduleDef, td, md, i, opMethod, iCount);
                }
            }

            if (i.OpCode == OpCodes.Stfld)
            {
                // this instruction sets the value of a field. cache the field reference.
                FieldDefinition opField = i.Operand as FieldDefinition;
                if (opField != null)
                {
                    ProcessInstructionSetterField(td, md, i, opField);
                }
            }

            if (i.OpCode == OpCodes.Ldfld)
            {
                // this instruction gets the value of a field. cache the field reference.
                FieldDefinition opField = i.Operand as FieldDefinition;
                if (opField != null)
                {
                    ProcessInstructionGetterField(td, md, i, opField);
                }
            }
        }

        // this is required to early-out from a function with "ref" or "out" parameters
        static void InjectGuardParameters(MethodDefinition md, ILProcessor worker, Instruction top)
        {
            int offset = md.Resolve().IsStatic ? 0 : 1;
            for (int index = 0; index < md.Parameters.Count; index++)
            {
                ParameterDefinition param = md.Parameters[index];
                if (param.IsOut)
                {
                    TypeReference elementType = param.ParameterType.GetElementType();
                    if (elementType.IsPrimitive)
                    {
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldarg, index + offset));
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldc_I4_0));
                        worker.InsertBefore(top, worker.Create(OpCodes.Stind_I4));
                    }
                    else
                    {
                        md.Body.Variables.Add(new VariableDefinition(elementType));
                        md.Body.InitLocals = true;

                        worker.InsertBefore(top, worker.Create(OpCodes.Ldarg, index + offset));
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldloca_S, (byte)(md.Body.Variables.Count - 1)));
                        worker.InsertBefore(top, worker.Create(OpCodes.Initobj, elementType));
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldloc, md.Body.Variables.Count - 1));
                        worker.InsertBefore(top, worker.Create(OpCodes.Stobj, elementType));
                    }
                }
            }
        }

        // this is required to early-out from a function with a return value.
        static void InjectGuardReturnValue(MethodDefinition md, ILProcessor worker, Instruction top)
        {
            if (md.ReturnType.FullName != voidType.FullName)
            {
                if (md.ReturnType.IsPrimitive)
                {
                    worker.InsertBefore(top, worker.Create(OpCodes.Ldc_I4_0));
                }
                else
                {
                    md.Body.Variables.Add(new VariableDefinition(md.ReturnType));
                    md.Body.InitLocals = true;

                    worker.InsertBefore(top, worker.Create(OpCodes.Ldloca_S, (byte)(md.Body.Variables.Count - 1)));
                    worker.InsertBefore(top, worker.Create(OpCodes.Initobj, md.ReturnType));
                    worker.InsertBefore(top, worker.Create(OpCodes.Ldloc, md.Body.Variables.Count - 1));
                }
            }
        }

        static void InjectServerGuard(ModuleDefinition moduleDef, TypeDefinition td, MethodDefinition md, bool logWarning)
        {
            if (!IsNetworkBehaviour(td))
            {
                Log.Error("[Server] guard on non-NetworkBehaviour script at [" + md.FullName + "]");
                return;
            }
            ILProcessor worker = md.Body.GetILProcessor();
            Instruction top = md.Body.Instructions[0];

            worker.InsertBefore(top, worker.Create(OpCodes.Call, NetworkServerGetActive));
            worker.InsertBefore(top, worker.Create(OpCodes.Brtrue, top));
            if (logWarning)
            {
                worker.InsertBefore(top, worker.Create(OpCodes.Ldstr, "[Server] function '" + md.FullName + "' called on client"));
                worker.InsertBefore(top, worker.Create(OpCodes.Call, logWarningReference));
            }
            InjectGuardParameters(md, worker, top);
            InjectGuardReturnValue(md, worker, top);
            worker.InsertBefore(top, worker.Create(OpCodes.Ret));
        }

        static void InjectClientGuard(ModuleDefinition moduleDef, TypeDefinition td, MethodDefinition md, bool logWarning)
        {
            if (!IsNetworkBehaviour(td))
            {
                Log.Error("[Client] guard on non-NetworkBehaviour script at [" + md.FullName + "]");
                return;
            }
            ILProcessor worker = md.Body.GetILProcessor();
            Instruction top = md.Body.Instructions[0];

            worker.InsertBefore(top, worker.Create(OpCodes.Call, NetworkClientGetActive));
            worker.InsertBefore(top, worker.Create(OpCodes.Brtrue, top));
            if (logWarning)
            {
                worker.InsertBefore(top, worker.Create(OpCodes.Ldstr, "[Client] function '" + md.FullName + "' called on server"));
                worker.InsertBefore(top, worker.Create(OpCodes.Call, logWarningReference));
            }

            InjectGuardParameters(md, worker, top);
            InjectGuardReturnValue(md, worker, top);
            worker.InsertBefore(top, worker.Create(OpCodes.Ret));
        }

        static void ProcessSiteMethod(ModuleDefinition moduleDef, TypeDefinition td, MethodDefinition md)
        {
            // process all references to replaced members with properties
            //Weaver.DLog(td, "      ProcessSiteMethod " + md);

            if (md.Name == ".cctor" ||
                md.Name == NetworkBehaviourProcessor.ProcessedFunctionName ||
                md.Name.StartsWith("CallCmd") ||
                md.Name.StartsWith("InvokeCmd") ||
                md.Name.StartsWith("InvokeRpc") ||
                md.Name.StartsWith("InvokeSyn"))
                return;

            if (md.Body != null && md.Body.Instructions != null)
            {
                foreach (CustomAttribute attr in md.CustomAttributes)
                {
                    switch (attr.Constructor.DeclaringType.ToString())
                    {
                        case "Mirror.ServerAttribute":
                            InjectServerGuard(moduleDef, td, md, true);
                            break;
                        case "Mirror.ServerCallbackAttribute":
                            InjectServerGuard(moduleDef, td, md, false);
                            break;
                        case "Mirror.ClientAttribute":
                            InjectClientGuard(moduleDef, td, md, true);
                            break;
                        case "Mirror.ClientCallbackAttribute":
                            InjectClientGuard(moduleDef, td, md, false);
                            break;
                    }
                }

                int iCount = 0;
                foreach (Instruction i in md.Body.Instructions)
                {
                    ProcessInstruction(moduleDef, td, md, i, iCount);
                    iCount += 1;
                }
            }
        }

        static void ProcessSiteClass(ModuleDefinition moduleDef, TypeDefinition td)
        {
            //Console.WriteLine("    ProcessSiteClass " + td);
            foreach (MethodDefinition md in td.Methods)
            {
                ProcessSiteMethod(moduleDef, td, md);
            }

            foreach (TypeDefinition nested in td.NestedTypes)
            {
                ProcessSiteClass(moduleDef, nested);
            }
        }

        static void ProcessSitesModule(ModuleDefinition moduleDef)
        {
            DateTime startTime = DateTime.Now;

            //Search through the types
            foreach (TypeDefinition td in moduleDef.Types)
            {
                if (td.IsClass)
                {
                    ProcessSiteClass(moduleDef, td);
                }
            }
            if (lists.generateContainerClass != null)
            {
                moduleDef.Types.Add(lists.generateContainerClass);
                scriptDef.MainModule.ImportReference(lists.generateContainerClass);

                foreach (MethodDefinition f in lists.generatedReadFunctions)
                {
                    scriptDef.MainModule.ImportReference(f);
                }

                foreach (MethodDefinition f in lists.generatedWriteFunctions)
                {
                    scriptDef.MainModule.ImportReference(f);
                }
            }
            Console.WriteLine("  ProcessSitesModule " + moduleDef.Name + " elapsed time:" + (DateTime.Now - startTime));
        }

        static void ProcessPropertySites()
        {
            ProcessSitesModule(scriptDef.MainModule);
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
            vector2Type = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Vector2");
            vector3Type = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Vector3");
            vector4Type = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Vector4");
            colorType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Color");
            color32Type = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Color32");
            quaternionType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Quaternion");
            rectType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Rect");
            planeType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Plane");
            rayType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Ray");
            matrixType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Matrix4x4");
            gameObjectType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.GameObject");
            transformType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Transform");
            unityObjectType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Object");

            NetworkClientType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.NetworkClient");
            NetworkServerType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.NetworkServer");

            SyncVarType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.SyncVarAttribute");
            CommandType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.CommandAttribute");
            ClientRpcType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.ClientRpcAttribute");
            TargetRpcType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.TargetRpcAttribute");
            SyncEventType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.SyncEventAttribute");
            SyncObjectType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.SyncObject");
        }

        static void SetupCorLib()
        {
            AssemblyNameReference name = AssemblyNameReference.Parse("mscorlib");
            ReaderParameters parameters = new ReaderParameters
            {
                AssemblyResolver = scriptDef.MainModule.AssemblyResolver,
            };
            corLib = scriptDef.MainModule.AssemblyResolver.Resolve(name, parameters).MainModule;
        }

        static TypeReference ImportCorLibType(string fullName)
        {
            TypeDefinition type = corLib.GetType(fullName) ?? corLib.ExportedTypes.First(t => t.FullName == fullName).Resolve();
            return scriptDef.MainModule.ImportReference(type);
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

            NetworkReaderType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.NetworkReader");
            NetworkReaderDef = NetworkReaderType.Resolve();

            NetworkReaderCtor = Resolvers.ResolveMethod(NetworkReaderDef, scriptDef, ".ctor");

            NetworkWriterType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.NetworkWriter");
            NetworkWriterDef  = NetworkWriterType.Resolve();

            NetworkWriterCtor = Resolvers.ResolveMethod(NetworkWriterDef, scriptDef, ".ctor");

            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, scriptDef, "get_active");
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, scriptDef, "get_localClientActive");
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, scriptDef, "get_active");

            NetworkReaderReadInt32 = Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadInt32");

            NetworkWriterWriteInt32 = Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", int32Type);
            NetworkWriterWriteInt16 = Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", int16Type);

            NetworkReaderReadPacked32 = Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadPackedUInt32");
            NetworkReaderReadPacked64 = Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadPackedUInt64");
            NetworkReaderReadByte = Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadByte");

            NetworkWriterWritePacked32 = Resolvers.ResolveMethod(NetworkWriterType, scriptDef, "WritePackedUInt32");
            NetworkWriterWritePacked64 = Resolvers.ResolveMethod(NetworkWriterType, scriptDef, "WritePackedUInt64");

            NetworkReadUInt16 = Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadUInt16");
            NetworkWriteUInt16 = Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", uint16Type);

            CmdDelegateReference = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.NetworkBehaviour/CmdDelegate");
            CmdDelegateConstructor = Resolvers.ResolveMethod(CmdDelegateReference, scriptDef, ".ctor");
            scriptDef.MainModule.ImportReference(gameObjectType);
            scriptDef.MainModule.ImportReference(transformType);

            TypeReference unetViewTmp = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.NetworkIdentity");
            NetworkIdentityType = scriptDef.MainModule.ImportReference(unetViewTmp);

            NetworkBehaviourType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.NetworkBehaviour");
            NetworkBehaviourType2 = scriptDef.MainModule.ImportReference(NetworkBehaviourType);
            NetworkConnectionType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.NetworkConnection");

            MonoBehaviourType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.MonoBehaviour");
            ScriptableObjectType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.ScriptableObject");

            NetworkConnectionType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.NetworkConnection");
            NetworkConnectionType = scriptDef.MainModule.ImportReference(NetworkConnectionType);

            ULocalConnectionToServerType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.ULocalConnectionToServer");
            ULocalConnectionToServerType = scriptDef.MainModule.ImportReference(ULocalConnectionToServerType);

            ULocalConnectionToClientType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.ULocalConnectionToClient");
            ULocalConnectionToClientType = scriptDef.MainModule.ImportReference(ULocalConnectionToClientType);

            MessageBaseType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.MessageBase");
            SyncListStructType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.SyncListSTRUCT`1");

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveProperty(NetworkBehaviourType, scriptDef, "syncVarDirtyBits");

            ComponentType = m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Component");
            ClientSceneType = m_UNetAssemblyDefinition.MainModule.GetType("Mirror.ClientScene");
            ReadyConnectionReference = Resolvers.ResolveMethod(ClientSceneType, scriptDef, "get_readyConnection");

            // get specialized GetComponent<NetworkIdentity>()
            getComponentReference = Resolvers.ResolveMethodGeneric(ComponentType, scriptDef, "GetComponent", NetworkIdentityType);

            getUNetIdReference = Resolvers.ResolveMethod(unetViewTmp, scriptDef, "get_netId");

            gameObjectInequality = Resolvers.ResolveMethod(unityObjectType, scriptDef, "op_Inequality");

            UBehaviourIsServer  = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "get_isServer");
            setSyncVarReference = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "SetSyncVar");
            setSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "set_syncVarHookGuard");
            getSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "get_syncVarHookGuard");

            setSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "SetSyncVarGameObject");
            getSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "GetSyncVarGameObject");
            setSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "SetSyncVarNetworkIdentity");
            getSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "GetSyncVarNetworkIdentity");
            registerCommandDelegateReference = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "RegisterCommandDelegate");
            registerRpcDelegateReference = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "RegisterRpcDelegate");
            registerEventDelegateReference = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "RegisterEventDelegate");
            getTypeReference = Resolvers.ResolveMethod(objectType, scriptDef, "GetType");
            getTypeFromHandleReference = Resolvers.ResolveMethod(typeType, scriptDef, "GetTypeFromHandle");
            logErrorReference = Resolvers.ResolveMethod(m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Debug"), scriptDef, "LogError");
            logWarningReference = Resolvers.ResolveMethod(m_UnityAssemblyDefinition.MainModule.GetType("UnityEngine.Debug"), scriptDef, "LogWarning");
            sendCommandInternal = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "SendCommandInternal");
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "SendRPCInternal");
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "SendTargetRPCInternal");
            sendEventInternal = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "SendEventInternal");

            SyncObjectType = scriptDef.MainModule.ImportReference(SyncObjectType);
            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, scriptDef, "InitSyncObject");
        }

        static void SetupReadFunctions()
        {
            lists.readFuncs = new Dictionary<string, MethodReference>
            {
                { singleType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadSingle") },
                { doubleType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadDouble") },
                { boolType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadBoolean") },
                { stringType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadString") },
                { int64Type.FullName, NetworkReaderReadPacked64 },
                { uint64Type.FullName, NetworkReaderReadPacked64 },
                { int32Type.FullName, NetworkReaderReadPacked32 },
                { uint32Type.FullName, NetworkReaderReadPacked32 },
                { int16Type.FullName, NetworkReaderReadPacked32 },
                { uint16Type.FullName, NetworkReaderReadPacked32 },
                { byteType.FullName, NetworkReaderReadPacked32 },
                { sbyteType.FullName, NetworkReaderReadPacked32 },
                { charType.FullName, NetworkReaderReadPacked32 },
                { decimalType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadDecimal") },
                { vector2Type.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadVector2") },
                { vector3Type.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadVector3") },
                { vector4Type.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadVector4") },
                { colorType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadColor") },
                { color32Type.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadColor32") },
                { quaternionType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadQuaternion") },
                { rectType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadRect") },
                { planeType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadPlane") },
                { rayType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadRay") },
                { matrixType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadMatrix4x4") },
                { guidType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadGuid") },
                { gameObjectType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadGameObject") },
                { NetworkIdentityType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadNetworkIdentity") },
                { transformType.FullName, Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadTransform") },
                { "System.Byte[]", Resolvers.ResolveMethod(NetworkReaderType, scriptDef, "ReadBytesAndSize") },
            };
        }

        static void SetupWriteFunctions()
        {
            lists.writeFuncs = new Dictionary<string, MethodReference>
            {
                { singleType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", singleType) },
                { doubleType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", doubleType) },
                { boolType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", boolType) },
                { stringType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", stringType) },
                { int64Type.FullName, NetworkWriterWritePacked64 },
                { uint64Type.FullName, NetworkWriterWritePacked64 },
                { int32Type.FullName, NetworkWriterWritePacked32 },
                { uint32Type.FullName, NetworkWriterWritePacked32 },
                { int16Type.FullName, NetworkWriterWritePacked32 },
                { uint16Type.FullName, NetworkWriterWritePacked32 },
                { byteType.FullName, NetworkWriterWritePacked32 },
                { sbyteType.FullName, NetworkWriterWritePacked32 },
                { charType.FullName, NetworkWriterWritePacked32 },
                { decimalType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", decimalType) },
                { vector2Type.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", vector2Type) },
                { vector3Type.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", vector3Type) },
                { vector4Type.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", vector4Type) },
                { colorType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", colorType) },
                { color32Type.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", color32Type) },
                { quaternionType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", quaternionType) },
                { rectType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", rectType) },
                { planeType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", planeType) },
                { rayType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", rayType) },
                { matrixType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", matrixType) },
                { guidType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", guidType) },
                { gameObjectType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", gameObjectType) },
                { NetworkIdentityType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", NetworkIdentityType) },
                { transformType.FullName, Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "Write", transformType) },
                { "System.Byte[]", Resolvers.ResolveMethodWithArg(NetworkWriterType, scriptDef, "WriteBytesAndSize", "System.Byte[]") }
            };
        }

        static bool IsNetworkBehaviour(TypeDefinition td)
        {
            return td.IsDerivedFrom(NetworkBehaviourType);
        }

        public static bool IsValidTypeToGenerate(TypeDefinition variable)
        {
            // a valid type is a simple class or struct. so we generate only code for types we dont know, and if they are not inside
            // this assembly it must mean that we are trying to serialize a variable outside our scope. and this will fail.

            string assembly = scriptDef.MainModule.Name;
            if (variable.Module.Name != assembly)
            {
                Log.Error("parameter [" + variable.Name +
                    "] is of the type [" +
                    variable.FullName +
                    "] is not a valid type, please make sure to use a valid type.");
                fail = true;
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

        static bool CheckSyncListStruct(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            bool didWork = false;

            // are ANY parent classes SyncListStruct
            TypeReference parent = td.BaseType;
            while (parent != null)
            {
                if (parent.FullName.StartsWith(SyncListStructType.FullName))
                {
                    SyncListStructProcessor.Process(td);
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
                didWork |= CheckSyncListStruct(embedded);
            }

            return didWork;
        }

        static bool Weave(string assName, IEnumerable<string> dependencies, IAssemblyResolver assemblyResolver, string unityEngineDLLPath, string unityUNetDLLPath, string outputDir)
        {
            ReaderParameters readParams = Helpers.ReaderParameters(assName, dependencies, assemblyResolver, unityEngineDLLPath, unityUNetDLLPath);
            scriptDef = AssemblyDefinition.ReadAssembly(assName, readParams);

            SetupTargetTypes();
            SetupReadFunctions();
            SetupWriteFunctions();

            ModuleDefinition moduleDefinition = scriptDef.MainModule;
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
                                didWork |= CheckSyncListStruct(td);
                            }
                            else
                            {
                                didWork |= CheckNetworkBehaviour(td);
                                didWork |= CheckMessageBase(td);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (scriptDef.MainModule.SymbolReader != null)
                                scriptDef.MainModule.SymbolReader.Dispose();
                            fail = true;
                            throw ex;
                        }
                    }

                    if (fail)
                    {
                        if (scriptDef.MainModule.SymbolReader != null)
                            scriptDef.MainModule.SymbolReader.Dispose();
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
                    if (scriptDef.MainModule.SymbolReader != null)
                        scriptDef.MainModule.SymbolReader.Dispose();
                    return false;
                }

                if (fail)
                {
                    //Log.Error("Failed phase II.");
                    if (scriptDef.MainModule.SymbolReader != null)
                        scriptDef.MainModule.SymbolReader.Dispose();
                    return false;
                }

                string dest = Helpers.DestinationFileFor(outputDir, assName);
                //Console.WriteLine ("Output:" + dest);

                WriterParameters writeParams = Helpers.GetWriterParameters(readParams);

                // PdbWriterProvider uses ISymUnmanagedWriter2 COM interface but Mono can't invoke a method on it and crashes (actually it first throws the following exception and then crashes).
                // One solution would be to convert UNetWeaver to exe file and run it on .NET on Windows (I have tested that and it works).
                // However it's much more simple to just write mdb file.
                // System.NullReferenceException: Object reference not set to an instance of an object
                //   at(wrapper cominterop - invoke) Mono.Cecil.Pdb.ISymUnmanagedWriter2:DefineDocument(string, System.Guid &, System.Guid &, System.Guid &, Mono.Cecil.Pdb.ISymUnmanagedDocumentWriter &)
                //   at Mono.Cecil.Pdb.SymWriter.DefineDocument(System.String url, Guid language, Guid languageVendor, Guid documentType)[0x00000] in < filename unknown >:0
                if (writeParams.SymbolWriterProvider is PdbWriterProvider)
                {
                    writeParams.SymbolWriterProvider = new MdbWriterProvider();
                    // old pdb file is out of date so delete it. symbols will be stored in mdb
                    string pdb = Path.ChangeExtension(assName, ".pdb");

                    try
                    {
                        File.Delete(pdb);
                    }
                    catch (Exception ex)
                    {
                        // workaround until Unity fixes C#7 compiler compability with the UNET weaver
                        UnityEngine.Debug.LogWarning(string.Format("Unable to delete file {0}: {1}", pdb, ex.Message));
                    }
                }

                scriptDef.Write(dest, writeParams);
            }

            if (scriptDef.MainModule.SymbolReader != null)
                scriptDef.MainModule.SymbolReader.Dispose();

            return true;
        }

        public static bool WeaveAssemblies(IEnumerable<string> assemblies, IEnumerable<string> dependencies, IAssemblyResolver assemblyResolver, string outputDir, string unityEngineDLLPath, string unityUNetDLLPath)
        {
            fail = false;
            lists = new WeaverLists();

            m_UnityAssemblyDefinition = AssemblyDefinition.ReadAssembly(unityEngineDLLPath);
            m_UNetAssemblyDefinition = AssemblyDefinition.ReadAssembly(unityUNetDLLPath);

            SetupUnityTypes();

            try
            {
                foreach (string ass in assemblies)
                {
                    if (!Weave(ass, dependencies, assemblyResolver, unityEngineDLLPath, unityUNetDLLPath, outputDir))
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
            corLib = null;
            return true;
        }
    }
}
