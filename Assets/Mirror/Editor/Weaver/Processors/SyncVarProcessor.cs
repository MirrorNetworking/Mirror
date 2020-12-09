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
        // ulong = 64 bytes
        const int SyncVarLimit = 64;


        static string HookParameterMessage(string hookName, TypeReference ValueType)
            => string.Format("void {0}({1} oldValue, {1} newValue)", hookName, ValueType);

        // Get hook method if any
        public static MethodDefinition GetHookMethod(TypeDefinition td, FieldDefinition syncVar)
        {
            CustomAttribute syncVarAttr = syncVar.GetCustomAttribute<SyncVarAttribute>();

            if (syncVarAttr == null)
                return null;

            string hookFunctionName = syncVarAttr.GetField<string>("hook", null);

            if (hookFunctionName == null)
                return null;

            return FindHookMethod(td, syncVar, hookFunctionName);
        }

        static MethodDefinition FindHookMethod(TypeDefinition td, FieldDefinition syncVar, string hookFunctionName)
        {
            List<MethodDefinition> methods = td.GetMethods(hookFunctionName);

            List<MethodDefinition> methodsWith2Param = new List<MethodDefinition>(methods.Where(m => m.Parameters.Count == 2));

            if (methodsWith2Param.Count == 0)
            {
                Weaver.Error($"Could not find hook for '{syncVar.Name}', hook name '{hookFunctionName}'. " +
                    $"Method signature should be {HookParameterMessage(hookFunctionName, syncVar.FieldType)}",
                    syncVar);

                return null;
            }

            foreach (MethodDefinition method in methodsWith2Param)
            {
                if (MatchesParameters(syncVar, method))
                {
                    return method;
                }
            }

            Weaver.Error($"Wrong type for Parameter in hook for '{syncVar.Name}', hook name '{hookFunctionName}'. " +
                     $"Method signature should be {HookParameterMessage(hookFunctionName, syncVar.FieldType)}",
                   syncVar);

            return null;
        }

        static bool MatchesParameters(FieldDefinition syncVar, MethodDefinition method)
        {
            // matches void onValueChange(T oldValue, T newValue)
            return method.Parameters[0].ParameterType.FullName == syncVar.FieldType.FullName &&
                   method.Parameters[1].ParameterType.FullName == syncVar.FieldType.FullName;
        }

        public static MethodDefinition GenerateSyncVarGetter(FieldDefinition fd, string originalName, FieldDefinition netFieldId)
        {
            //Create the get method
            MethodDefinition get = new MethodDefinition(
                    "get_Network" + originalName, MethodAttributes.Public |
                    MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig,
                    fd.FieldType);

            ILProcessor worker = get.Body.GetILProcessor();

            // [SyncVar] GameObject?
            if (fd.FieldType.Is<UnityEngine.GameObject>())
            {
                // return this.GetSyncVarGameObject(ref field, uint netId);
                // this.
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldfld, netFieldId));
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldflda, fd));
                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.getSyncVarGameObjectReference));
                worker.Append(worker.Create(OpCodes.Ret));
            }
            // [SyncVar] NetworkIdentity?
            else if (fd.FieldType.Is<NetworkIdentity>())
            {
                // return this.GetSyncVarNetworkIdentity(ref field, uint netId);
                // this.
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldfld, netFieldId));
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldflda, fd));
                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.getSyncVarNetworkIdentityReference));
                worker.Append(worker.Create(OpCodes.Ret));
            }
            else if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                // return this.GetSyncVarNetworkBehaviour<T>(ref field, uint netId);
                // this.
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldfld, netFieldId));
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldflda, fd));
                MethodReference getFunc = WeaverTypes.getSyncVarNetworkBehaviourReference.MakeGeneric(fd.FieldType);
                worker.Append(worker.Create(OpCodes.Call, getFunc));
                worker.Append(worker.Create(OpCodes.Ret));
            }
            // [SyncVar] int, string, etc.
            else
            {
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldfld, fd));
                worker.Append(worker.Create(OpCodes.Ret));
            }

            get.Body.Variables.Add(new VariableDefinition(fd.FieldType));
            get.Body.InitLocals = true;
            get.SemanticsAttributes = MethodSemanticsAttributes.Getter;

            return get;
        }

        public static MethodDefinition GenerateSyncVarSetter(TypeDefinition td, FieldDefinition fd, string originalName, long dirtyBit, FieldDefinition netFieldId)
        {
            //Create the set method
            MethodDefinition set = new MethodDefinition("set_Network" + originalName, MethodAttributes.Public |
                    MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig,
                    WeaverTypes.Import(typeof(void)));

            ILProcessor worker = set.Body.GetILProcessor();

            // if (!SyncVarEqual(value, ref playerData))
            Instruction endOfMethod = worker.Create(OpCodes.Nop);

            // this
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            // new value to set
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            // reference to field to set
            // make generic version of SetSyncVar with field type
            if (fd.FieldType.Is<UnityEngine.GameObject>())
            {
                // reference to netId Field to set
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldfld, netFieldId));

                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.syncVarGameObjectEqualReference));
            }
            else if (fd.FieldType.Is<NetworkIdentity>())
            {
                // reference to netId Field to set
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldfld, netFieldId));

                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.syncVarNetworkIdentityEqualReference));
            }
            else if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                // reference to netId Field to set
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldfld, netFieldId));

                MethodReference getFunc = WeaverTypes.syncVarNetworkBehaviourEqualReference.MakeGeneric(fd.FieldType);
                worker.Append(worker.Create(OpCodes.Call, getFunc));
            }
            else
            {
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldflda, fd));

                GenericInstanceMethod syncVarEqualGm = new GenericInstanceMethod(WeaverTypes.syncVarEqualReference);
                syncVarEqualGm.GenericArguments.Add(fd.FieldType);
                worker.Append(worker.Create(OpCodes.Call, syncVarEqualGm));
            }

            worker.Append(worker.Create(OpCodes.Brtrue, endOfMethod));

            // T oldValue = value;
            // TODO for GO/NI we need to backup the netId don't we?
            VariableDefinition oldValue = new VariableDefinition(fd.FieldType);
            set.Body.Variables.Add(oldValue);
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldfld, fd));
            worker.Append(worker.Create(OpCodes.Stloc, oldValue));

            // this
            worker.Append(worker.Create(OpCodes.Ldarg_0));

            // new value to set
            worker.Append(worker.Create(OpCodes.Ldarg_1));

            // reference to field to set
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldflda, fd));

            // dirty bit
            // 8 byte integer aka long
            worker.Append(worker.Create(OpCodes.Ldc_I8, dirtyBit));

            if (fd.FieldType.Is<UnityEngine.GameObject>())
            {
                // reference to netId Field to set
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldflda, netFieldId));

                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.setSyncVarGameObjectReference));
            }
            else if (fd.FieldType.Is<NetworkIdentity>())
            {
                // reference to netId Field to set
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldflda, netFieldId));

                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.setSyncVarNetworkIdentityReference));
            }
            else if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                // reference to netId Field to set
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldflda, netFieldId));

                MethodReference getFunc = WeaverTypes.setSyncVarNetworkBehaviourReference.MakeGeneric(fd.FieldType);
                worker.Append(worker.Create(OpCodes.Call, getFunc));
            }
            else
            {
                // make generic version of SetSyncVar with field type
                GenericInstanceMethod gm = new GenericInstanceMethod(WeaverTypes.setSyncVarReference);
                gm.GenericArguments.Add(fd.FieldType);

                // invoke SetSyncVar
                worker.Append(worker.Create(OpCodes.Call, gm));
            }

            MethodDefinition hookMethod = GetHookMethod(td, fd);

            if (hookMethod != null)
            {
                //if (NetworkServer.localClientActive && !getSyncVarHookGuard(dirtyBit))
                Instruction label = worker.Create(OpCodes.Nop);
                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.NetworkServerGetLocalClientActive));
                worker.Append(worker.Create(OpCodes.Brfalse, label));
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldc_I8, dirtyBit));
                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.getSyncVarHookGuard));
                worker.Append(worker.Create(OpCodes.Brtrue, label));

                // setSyncVarHookGuard(dirtyBit, true);
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldc_I8, dirtyBit));
                worker.Append(worker.Create(OpCodes.Ldc_I4_1));
                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.setSyncVarHookGuard));

                // call hook (oldValue, newValue)
                // Generates: OnValueChanged(oldValue, value);
                WriteCallHookMethodUsingArgument(worker, hookMethod, oldValue);

                // setSyncVarHookGuard(dirtyBit, false);
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldc_I8, dirtyBit));
                worker.Append(worker.Create(OpCodes.Ldc_I4_0));
                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.setSyncVarHookGuard));

                worker.Append(label);
            }

            worker.Append(endOfMethod);

            worker.Append(worker.Create(OpCodes.Ret));

            set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.In, fd.FieldType));
            set.SemanticsAttributes = MethodSemanticsAttributes.Setter;

            return set;
        }

        public static void ProcessSyncVar(TypeDefinition td, FieldDefinition fd, Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds, long dirtyBit)
        {
            string originalName = fd.Name;
            Weaver.DLog(td, "Sync Var " + fd.Name + " " + fd.FieldType);

            // GameObject/NetworkIdentity SyncVars have a new field for netId
            FieldDefinition netIdField = null;
            // NetworkBehaviour has different field type than other NetworkIdentityFields
            if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                netIdField = new FieldDefinition("___" + fd.Name + "NetId",
                   FieldAttributes.Private,
                   WeaverTypes.Import<NetworkBehaviour.NetworkBehaviourSyncVar>());

                syncVarNetIds[fd] = netIdField;
            }
            else if (fd.FieldType.IsNetworkIdentityField())
            {
                netIdField = new FieldDefinition("___" + fd.Name + "NetId",
                    FieldAttributes.Private,
                    WeaverTypes.Import<uint>());

                syncVarNetIds[fd] = netIdField;
            }

            MethodDefinition get = GenerateSyncVarGetter(fd, originalName, netIdField);
            MethodDefinition set = GenerateSyncVarSetter(td, fd, originalName, dirtyBit, netIdField);

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
            if (fd.FieldType.IsNetworkIdentityField())
            {
                Weaver.WeaveLists.replacementGetterProperties[fd] = get;
            }
        }

        public static (List<FieldDefinition> syncVars, Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds) ProcessSyncVars(TypeDefinition td)
        {
            List<FieldDefinition> syncVars = new List<FieldDefinition>();
            Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds = new Dictionary<FieldDefinition, FieldDefinition>();

            // the mapping of dirtybits to sync-vars is implicit in the order of the fields here. this order is recorded in m_replacementProperties.
            // start assigning syncvars at the place the base class stopped, if any
            int dirtyBitCounter = Weaver.WeaveLists.GetSyncVarStart(td.BaseType.FullName);

            // find syncvars
            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.HasCustomAttribute<SyncVarAttribute>())
                {
                    if ((fd.Attributes & FieldAttributes.Static) != 0)
                    {
                        Weaver.Error($"{fd.Name} cannot be static", fd);
                        continue;
                    }

                    if (fd.FieldType.IsArray)
                    {
                        Weaver.Error($"{fd.Name} has invalid type. Use SyncLists instead of arrays", fd);
                        continue;
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

                        if (dirtyBitCounter == SyncVarLimit)
                        {
                            Weaver.Error($"{td.Name} has too many SyncVars. Consider refactoring your class into multiple components", td);
                            continue;
                        }
                    }
                }
            }

            // add all the new SyncVar __netId fields
            foreach (FieldDefinition fd in syncVarNetIds.Values)
            {
                td.Fields.Add(fd);
            }
            Weaver.WeaveLists.SetNumSyncVars(td.FullName, syncVars.Count);

            return (syncVars, syncVarNetIds);
        }

        public static void WriteCallHookMethodUsingArgument(ILProcessor worker, MethodDefinition hookMethod, VariableDefinition oldValue)
        {
            WriteCallHookMethod(worker, hookMethod, oldValue, null);
        }

        public static void WriteCallHookMethodUsingField(ILProcessor worker, MethodDefinition hookMethod, VariableDefinition oldValue, FieldDefinition newValue)
        {
            if (newValue == null)
            {
                Weaver.Error("NewValue field was null when writing SyncVar hook");
            }

            WriteCallHookMethod(worker, hookMethod, oldValue, newValue);
        }

        static void WriteCallHookMethod(ILProcessor worker, MethodDefinition hookMethod, VariableDefinition oldValue, FieldDefinition newValue)
        {
            WriteStartFunctionCall();

            // write args
            WriteOldValue();
            WriteNewValue();

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

            // Writes this before method if it is not static
            void WriteStartFunctionCall()
            {
                // dont add this (Ldarg_0) if method is static
                if (!hookMethod.IsStatic)
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
                OpCode opcode = hookMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt;
                worker.Append(worker.Create(opcode, hookMethod));
            }
        }
    }
}
