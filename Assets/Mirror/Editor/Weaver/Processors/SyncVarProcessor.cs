using System;
using System.Collections.Generic;
using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    /// <summary>
    /// Processes [SyncVar] in NetworkBehaviour
    /// </summary>
    public static class SyncVarProcessor
    {
        /// <summary>
        /// <see cref="Mirror.HookParameter"/>
        /// </summary>
        public enum HookParameter
        {
            AutoDetect = 0,
            // numbers match parameter count, do not change
            New = 1,
            OldNew = 2,
            OldNewInitial = 3
        }

        public struct GetHookResult
        {
            public bool found;
            public HookParameter parameter;
            public MethodDefinition method;

            public static readonly GetHookResult NotFound = new GetHookResult { found = false };
        }

        // used to log expected method signature
        static string HookImplementationMessage(string hookName, string ValueType)
        {
            return "Hook method should have one of the following signatures\n" +
                HookParameterMessage(HookParameter.New, hookName, ValueType) + "\n" +
                HookParameterMessage(HookParameter.OldNew, hookName, ValueType) + "\n" +
                HookParameterMessage(HookParameter.OldNewInitial, hookName, ValueType);
        }
        static string HookParameterMessage(HookParameter hookParameter, string hookName, string ValueType)
        {
            switch (hookParameter)
            {
                case HookParameter.New:
                    return string.Format("void {0}({1} newValue)", hookName, ValueType);
                case HookParameter.OldNew:
                    return string.Format("void {0}({1} oldValue, {1} newValue)", hookName, ValueType);
                case HookParameter.OldNewInitial:
                    return string.Format("void {0}({1} oldValue, {1} newValue, bool initialState)", hookName, ValueType);
                default:
                case HookParameter.AutoDetect:
                    // No format for AutoDetect
                    return "";
            }
        }

        // ulong = 64 bytes
        const int SyncVarLimit = 64;

        // Get hook method if any
        public static GetHookResult GetHookMethod(TypeDefinition td, FieldDefinition syncVar)
        {
            CustomAttribute ca = syncVar.GetCustomAttribute(Weaver.SyncVarType.FullName);

            if (ca == null)
                return GetHookResult.NotFound;

            string hookFunctionName = ca.GetField<string>("hook", null);
            HookParameter hookParameter = ca.GetField("hookParameter", HookParameter.AutoDetect);

            if (hookFunctionName == null)
                return GetHookResult.NotFound;

            return FindHookMethod(td, syncVar, hookFunctionName, hookParameter);
        }

        static GetHookResult FindHookMethod(TypeDefinition td, FieldDefinition syncVar, string hookFunctionName, HookParameter hookParameter)
        {
            List<MethodDefinition> methods = td.GetMethods(hookFunctionName);

            if (hookParameter == HookParameter.AutoDetect)
            {
                return AutoDetectHookMethod(syncVar, hookFunctionName, methods);
            }
            else
            {
                return FindHookWithParameters(syncVar, hookFunctionName, methods, hookParameter);
            }
        }

        static GetHookResult AutoDetectHookMethod(FieldDefinition syncVar, string hookFunctionName, List<MethodDefinition> methods)
        {
            List<MethodDefinition> methodsWith1Param = new List<MethodDefinition>(methods.Where(m => m.Parameters.Count == 1));
            List<MethodDefinition> methodsWith2Param = new List<MethodDefinition>(methods.Where(m => m.Parameters.Count == 2));
            List<MethodDefinition> methodsWith3Param = new List<MethodDefinition>(methods.Where(m => m.Parameters.Count == 3));

            // If all empty return early with ErrorMessage
            if (methodsWith1Param.Count == 0 &&
                methodsWith2Param.Count == 0 &&
                methodsWith3Param.Count == 0)
            {
                Weaver.Error($"No hook with correct parameters found for '{syncVar.Name}', hook name '{hookFunctionName}'. {HookImplementationMessage(hookFunctionName, syncVar.FieldType.ToString())}",
                    syncVar);
                return GetHookResult.NotFound;
            }

            string multipleFoundError = $"Multiple hooks found for for '{syncVar.Name}', hook name '{hookFunctionName}'. " +
                $"Use the hookParameter option in the SyncVar Attribute to pick which one to use.";

            // Find method with matching parameters
            // return error if multiple are found
            MethodDefinition match = null;
            HookParameter matchParameters = HookParameter.AutoDetect;

            foreach (MethodDefinition method in methodsWith1Param)
            {
                if (MatchesParameters(HookParameter.New, syncVar, method))
                {
                    if (match != null)
                    {
                        Weaver.Error(multipleFoundError, syncVar);
                        return GetHookResult.NotFound;
                    }

                    match = method;
                    matchParameters = HookParameter.New;
                }
            }

            foreach (MethodDefinition method in methodsWith2Param)
            {
                if (MatchesParameters(HookParameter.OldNew, syncVar, method))
                {
                    if (match != null)
                    {
                        Weaver.Error(multipleFoundError, syncVar);
                        return GetHookResult.NotFound;
                    }

                    match = method;
                    matchParameters = HookParameter.OldNew;
                }
            }

            foreach (MethodDefinition method in methodsWith3Param)
            {
                if (MatchesParameters(HookParameter.OldNewInitial, syncVar, method))
                {
                    if (match != null)
                    {
                        Weaver.Error(multipleFoundError, syncVar);
                        return GetHookResult.NotFound;
                    }

                    match = method;
                    matchParameters = HookParameter.OldNewInitial;
                }
            }

            // If only 1 found, return it
            if (match != null)
            {
                return new GetHookResult
                {
                    found = true,
                    method = match,
                    parameter = matchParameters
                };
            }
            else
            {
                Weaver.Error($"No hook with correct parameters found for '{syncVar.Name}', hook name '{hookFunctionName}'. {HookImplementationMessage(hookFunctionName, syncVar.FieldType.ToString())}",
                    syncVar);
                return GetHookResult.NotFound;
            }
        }

        static GetHookResult FindHookWithParameters(FieldDefinition syncVar, string hookFunctionName, List<MethodDefinition> methods, HookParameter hookParameter)
        {
            int parameterCount = (int)hookParameter;
            List<MethodDefinition> methodsWithParamCount = new List<MethodDefinition>(methods.Where(m => m.Parameters.Count == parameterCount));

            if (methodsWithParamCount.Count == 0)
            {
                Weaver.Error($"Could not find hook for '{syncVar.Name}', hook name '{hookFunctionName}' with '{Enum.GetName(typeof(HookParameter), hookParameter)}' parameters. " +
                    $"Method signature should be {HookParameterMessage(hookParameter, hookFunctionName, syncVar.FieldType.ToString())}",
                    syncVar);
                return GetHookResult.NotFound;
            }

            foreach (MethodDefinition method in methodsWithParamCount)
            {
                if (MatchesParameters(hookParameter, syncVar, method))
                {
                    return new GetHookResult
                    {
                        found = true,
                        method = method,
                        parameter = hookParameter
                    };
                }
            }

            Weaver.Error($"Wrong type for Parameter in hook for '{syncVar.Name}', hook name '{hookFunctionName}' with '{Enum.GetName(typeof(HookParameter), hookParameter)}' parameters. " +
               $"Method signature should be {HookParameterMessage(hookParameter, hookFunctionName, syncVar.FieldType.ToString())}",
               syncVar);
            return GetHookResult.NotFound;
        }

        static bool MatchesParameters(HookParameter hookParameter, FieldDefinition syncVar, MethodDefinition method)
        {
            if (hookParameter == HookParameter.New)
            {
                // matches void onValueChange(T newValue)
                return method.Parameters[0].ParameterType.FullName == syncVar.FieldType.FullName;
            }
            else if (hookParameter == HookParameter.OldNew)
            {
                // matches void onValueChange(T oldValue, T newValue)
                return method.Parameters[0].ParameterType.FullName == syncVar.FieldType.FullName &&
                       method.Parameters[1].ParameterType.FullName == syncVar.FieldType.FullName;
            }
            else if (hookParameter == HookParameter.OldNewInitial)
            {
                // matches void onValueChange(T oldValue, T newValue, bool initialState)
                return method.Parameters[0].ParameterType.FullName == syncVar.FieldType.FullName &&
                       method.Parameters[1].ParameterType.FullName == syncVar.FieldType.FullName &&
                       method.Parameters[2].ParameterType.FullName == Weaver.boolType.FullName;
            }

            throw new ArgumentException("hookParameter should not be autodetect for MatchesParameters", nameof(hookParameter));
        }

        public static MethodDefinition ProcessSyncVarGet(FieldDefinition fd, string originalName, FieldDefinition netFieldId)
        {
            //Create the get method
            MethodDefinition get = new MethodDefinition(
                    "get_Network" + originalName, MethodAttributes.Public |
                    MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig,
                    fd.FieldType);

            ILProcessor getWorker = get.Body.GetILProcessor();

            // [SyncVar] GameObject?
            if (fd.FieldType.FullName == Weaver.gameObjectType.FullName)
            {
                // return this.GetSyncVarGameObject(ref field, uint netId);
                // this.
                getWorker.Append(getWorker.Create(OpCodes.Ldarg_0));
                getWorker.Append(getWorker.Create(OpCodes.Ldarg_0));
                getWorker.Append(getWorker.Create(OpCodes.Ldfld, netFieldId));
                getWorker.Append(getWorker.Create(OpCodes.Ldarg_0));
                getWorker.Append(getWorker.Create(OpCodes.Ldflda, fd));
                getWorker.Append(getWorker.Create(OpCodes.Call, Weaver.getSyncVarGameObjectReference));
                getWorker.Append(getWorker.Create(OpCodes.Ret));
            }
            // [SyncVar] NetworkIdentity?
            else if (fd.FieldType.FullName == Weaver.NetworkIdentityType.FullName)
            {
                // return this.GetSyncVarNetworkIdentity(ref field, uint netId);
                // this.
                getWorker.Append(getWorker.Create(OpCodes.Ldarg_0));
                getWorker.Append(getWorker.Create(OpCodes.Ldarg_0));
                getWorker.Append(getWorker.Create(OpCodes.Ldfld, netFieldId));
                getWorker.Append(getWorker.Create(OpCodes.Ldarg_0));
                getWorker.Append(getWorker.Create(OpCodes.Ldflda, fd));
                getWorker.Append(getWorker.Create(OpCodes.Call, Weaver.getSyncVarNetworkIdentityReference));
                getWorker.Append(getWorker.Create(OpCodes.Ret));
            }
            // [SyncVar] int, string, etc.
            else
            {
                getWorker.Append(getWorker.Create(OpCodes.Ldarg_0));
                getWorker.Append(getWorker.Create(OpCodes.Ldfld, fd));
                getWorker.Append(getWorker.Create(OpCodes.Ret));
            }

            get.Body.Variables.Add(new VariableDefinition(fd.FieldType));
            get.Body.InitLocals = true;
            get.SemanticsAttributes = MethodSemanticsAttributes.Getter;

            return get;
        }

        public static MethodDefinition ProcessSyncVarSet(TypeDefinition td, FieldDefinition fd, string originalName, long dirtyBit, FieldDefinition netFieldId)
        {
            //Create the set method
            MethodDefinition set = new MethodDefinition("set_Network" + originalName, MethodAttributes.Public |
                    MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            ILProcessor setWorker = set.Body.GetILProcessor();

            // if (!SyncVarEqual(value, ref playerData))
            Instruction endOfMethod = setWorker.Create(OpCodes.Nop);

            // this
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
            // new value to set
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_1));
            // reference to field to set
            // make generic version of SetSyncVar with field type
            if (fd.FieldType.FullName == Weaver.gameObjectType.FullName)
            {
                // reference to netId Field to set
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldfld, netFieldId));

                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.syncVarGameObjectEqualReference));
            }
            else if (fd.FieldType.FullName == Weaver.NetworkIdentityType.FullName)
            {
                // reference to netId Field to set
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldfld, netFieldId));

                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.syncVarNetworkIdentityEqualReference));
            }
            else
            {
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldflda, fd));

                GenericInstanceMethod syncVarEqualGm = new GenericInstanceMethod(Weaver.syncVarEqualReference);
                syncVarEqualGm.GenericArguments.Add(fd.FieldType);
                setWorker.Append(setWorker.Create(OpCodes.Call, syncVarEqualGm));
            }

            setWorker.Append(setWorker.Create(OpCodes.Brtrue, endOfMethod));

            // T oldValue = value;
            // TODO for GO/NI we need to backup the netId don't we?
            VariableDefinition oldValue = new VariableDefinition(fd.FieldType);
            set.Body.Variables.Add(oldValue);
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
            setWorker.Append(setWorker.Create(OpCodes.Ldfld, fd));
            setWorker.Append(setWorker.Create(OpCodes.Stloc, oldValue));

            // this
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));

            // new value to set
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_1));

            // reference to field to set
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
            setWorker.Append(setWorker.Create(OpCodes.Ldflda, fd));

            // dirty bit
            // 8 byte integer aka long
            setWorker.Append(setWorker.Create(OpCodes.Ldc_I8, dirtyBit));

            if (fd.FieldType.FullName == Weaver.gameObjectType.FullName)
            {
                // reference to netId Field to set
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldflda, netFieldId));

                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.setSyncVarGameObjectReference));
            }
            else if (fd.FieldType.FullName == Weaver.NetworkIdentityType.FullName)
            {
                // reference to netId Field to set
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldflda, netFieldId));

                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.setSyncVarNetworkIdentityReference));
            }
            else
            {
                // make generic version of SetSyncVar with field type
                GenericInstanceMethod gm = new GenericInstanceMethod(Weaver.setSyncVarReference);
                gm.GenericArguments.Add(fd.FieldType);

                // invoke SetSyncVar
                setWorker.Append(setWorker.Create(OpCodes.Call, gm));
            }

            GetHookResult hookResult = GetHookMethod(td, fd);

            if (hookResult.found)
            {
                //if (NetworkServer.localClientActive && !getSyncVarHookGuard(dirtyBit))
                Instruction label = setWorker.Create(OpCodes.Nop);
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.NetworkServerGetLocalClientActive));
                setWorker.Append(setWorker.Create(OpCodes.Brfalse, label));
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldc_I8, dirtyBit));
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.getSyncVarHookGuard));
                setWorker.Append(setWorker.Create(OpCodes.Brtrue, label));

                // setSyncVarHookGuard(dirtyBit, true);
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldc_I8, dirtyBit));
                setWorker.Append(setWorker.Create(OpCodes.Ldc_I4_1));
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.setSyncVarHookGuard));

                // call hook (oldValue, newValue)
                WriteCallHookMethodUsingArgument(setWorker, hookResult, oldValue);

                // setSyncVarHookGuard(dirtyBit, false);
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldc_I8, dirtyBit));
                setWorker.Append(setWorker.Create(OpCodes.Ldc_I4_0));
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.setSyncVarHookGuard));

                setWorker.Append(label);
            }

            setWorker.Append(endOfMethod);

            setWorker.Append(setWorker.Create(OpCodes.Ret));

            set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.In, fd.FieldType));
            set.SemanticsAttributes = MethodSemanticsAttributes.Setter;

            return set;
        }

        public static void ProcessSyncVar(TypeDefinition td, FieldDefinition fd, Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds, long dirtyBit)
        {
            string originalName = fd.Name;
            Weaver.DLog(td, "Sync Var " + fd.Name + " " + fd.FieldType + " " + Weaver.gameObjectType);

            // GameObject/NetworkIdentity SyncVars have a new field for netId
            FieldDefinition netIdField = null;
            if (fd.FieldType.FullName == Weaver.gameObjectType.FullName ||
                fd.FieldType.FullName == Weaver.NetworkIdentityType.FullName)
            {
                netIdField = new FieldDefinition("___" + fd.Name + "NetId",
                    FieldAttributes.Private,
                    Weaver.uint32Type);

                syncVarNetIds[fd] = netIdField;
            }

            MethodDefinition get = ProcessSyncVarGet(fd, originalName, netIdField);
            MethodDefinition set = ProcessSyncVarSet(td, fd, originalName, dirtyBit, netIdField);

            //NOTE: is property even needed? Could just use a setter function?
            //create the property
            PropertyDefinition propertyDefinition = new PropertyDefinition("Network" + originalName, PropertyAttributes.None, fd.FieldType)
            {
                GetMethod = get,
                SetMethod = set
            };

            //add the methods and property to the type.
            td.Methods.Add(get);
            td.Methods.Add(set);
            td.Properties.Add(propertyDefinition);
            Weaver.WeaveLists.replacementSetterProperties[fd] = set;

            // replace getter field if GameObject/NetworkIdentity so it uses
            // netId instead
            // -> only for GameObjects, otherwise an int syncvar's getter would
            //    end up in recursion.
            if (fd.FieldType.FullName == Weaver.gameObjectType.FullName ||
                fd.FieldType.FullName == Weaver.NetworkIdentityType.FullName)
            {
                Weaver.WeaveLists.replacementGetterProperties[fd] = get;
            }
        }

        public static void ProcessSyncVars(TypeDefinition td, List<FieldDefinition> syncVars, List<FieldDefinition> syncObjects, Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds)
        {
            int numSyncVars = 0;

            // the mapping of dirtybits to sync-vars is implicit in the order of the fields here. this order is recorded in m_replacementProperties.
            // start assigning syncvars at the place the base class stopped, if any
            int dirtyBitCounter = Weaver.GetSyncVarStart(td.BaseType.FullName);

            syncVarNetIds.Clear();

            // find syncvars
            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.HasCustomAttribute(Weaver.SyncVarType))
                {
                    if ((fd.Attributes & FieldAttributes.Static) != 0)
                    {
                        Weaver.Error($"{fd.Name} cannot be static", fd);
                        return;
                    }

                    if (fd.FieldType.IsArray)
                    {
                        Weaver.Error($"{fd.Name} has invalid type. Use SyncLists instead of arrays", fd);
                        return;
                    }

                    if (SyncObjectInitializer.ImplementsSyncObject(fd.FieldType))
                    {
                        Weaver.Warning($"{fd.Name} has [SyncVar] attribute. SyncLists should not be marked with SyncVar", fd);
                    }
                    else
                    {
                        syncVars.Add(fd);

                        ProcessSyncVar(td, fd, syncVarNetIds, 1L << dirtyBitCounter);
                        dirtyBitCounter += 1;
                        numSyncVars += 1;

                        if (dirtyBitCounter == SyncVarLimit)
                        {
                            Weaver.Error($"{td.Name} has too many SyncVars. Consider refactoring your class into multiple components", td);
                            return;
                        }
                    }
                }

                if (fd.FieldType.Resolve().ImplementsInterface(Weaver.SyncObjectType))
                {
                    if (fd.IsStatic)
                    {
                        Weaver.Error($"{fd.Name} cannot be static", fd);
                        return;
                    }

                    if (fd.FieldType.Resolve().HasGenericParameters)
                    {
                        Weaver.Error($"Cannot use generic SyncObject {fd.Name} directly in NetworkBehaviour. Create a class and inherit from the generic SyncObject instead", fd);
                        return;
                    }

                    syncObjects.Add(fd);
                }
            }

            // add all the new SyncVar __netId fields
            foreach (FieldDefinition fd in syncVarNetIds.Values)
            {
                td.Fields.Add(fd);
            }

            Weaver.SetNumSyncVars(td.FullName, numSyncVars);
        }

        public static void WriteCallHookMethodUsingArgument(ILProcessor worker, GetHookResult hookResult, VariableDefinition oldValue)
        {
            _WriteCallHookMethod(worker, hookResult, oldValue, null, false);
        }

        public static void WriteCallHookMethodUsingField(ILProcessor worker, GetHookResult hookResult, VariableDefinition oldValue, FieldDefinition newValue, bool initialState)
        {
            if (newValue == null)
            {
                Weaver.Error("NewValue field was null when writing SyncVar hook");
            }

            _WriteCallHookMethod(worker, hookResult, oldValue, newValue, initialState);
        }

        static void _WriteCallHookMethod(ILProcessor worker, GetHookResult hookResult, VariableDefinition oldValue, FieldDefinition newValue, bool initialState)
        {
            WriteStartFunctionCall();

            // write args
            if (hookResult.parameter == HookParameter.New)
            {
                WriteNewValue();
            }
            else if (hookResult.parameter == HookParameter.OldNew)
            {
                WriteOldValue();
                WriteNewValue();
            }
            else if (hookResult.parameter == HookParameter.OldNewInitial)
            {
                WriteOldValue();
                WriteNewValue();
                WriteInitialState();
            }
            else
            {
                Weaver.Error("Invalid hook parameter in WriteCallHookMethod");
            }

            WriteEndFunctionCall();


            // *** Local functions used to write OpCodes ***
            // Local functions have access to function variables, no need to pass in args

            void WriteOldValue()
            {
                worker.Append(worker.Create(OpCodes.Ldloc, oldValue));
            }

            void WriteNewValue()
            {
                // write arg1 or this.field
                if (newValue == null)
                {
                    worker.Append(worker.Create(OpCodes.Ldarg_1));
                }
                else
                {
                    // this.
                    worker.Append(worker.Create(OpCodes.Ldarg_0));
                    // syncvar.get
                    worker.Append(worker.Create(OpCodes.Ldfld, newValue));
                }
            }

            void WriteInitialState()
            {
                worker.Append(worker.Create(initialState ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            }

            // Writes this before method if it is not static
            void WriteStartFunctionCall()
            {
                // dont add this (Ldarg_0) if method is static
                if (!hookResult.method.IsStatic)
                {
                    // this before method call
                    // eg this.onValueChanged
                    worker.Append(worker.Create(OpCodes.Ldarg_0));
                }
            }

            // Calls method
            void WriteEndFunctionCall()
            {
                // only use Callvirt when not static
                OpCode opcode = hookResult.method.IsStatic ? OpCodes.Call : OpCodes.Callvirt;
                worker.Append(worker.Create(opcode, hookResult.method));
            }
        }
    }
}
