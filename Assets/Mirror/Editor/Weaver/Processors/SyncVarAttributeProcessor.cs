using System;
using System.Collections.Generic;
using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;
using Mono.CecilX.Rocks;

namespace Mirror.Weaver
{
    // Processes [SyncVar] attribute fields in NetworkBehaviour
    // not static, because ILPostProcessor is multithreaded
    public class SyncVarAttributeProcessor
    {
        // ulong = 64 bytes
        const int SyncVarLimit = 64;

        AssemblyDefinition assembly;
        WeaverTypes weaverTypes;
        SyncVarAccessLists syncVarAccessLists;
        Logger Log;

        string HookParameterMessage(string hookName, TypeReference ValueType) =>
            $"void {hookName}({ValueType} oldValue, {ValueType} newValue)";

        public SyncVarAttributeProcessor(AssemblyDefinition assembly, WeaverTypes weaverTypes, SyncVarAccessLists syncVarAccessLists, Logger Log)
        {
            this.assembly = assembly;
            this.weaverTypes = weaverTypes;
            this.syncVarAccessLists = syncVarAccessLists;
            this.Log = Log;
        }

        // Get hook method if any
        public MethodDefinition GetHookMethod(TypeDefinition td, FieldDefinition syncVar, ref bool WeavingFailed)
        {
            CustomAttribute syncVarAttr = syncVar.GetCustomAttribute<SyncVarAttribute>();

            if (syncVarAttr == null)
                return null;

            string hookFunctionName = syncVarAttr.GetField<string>("hook", null);

            if (hookFunctionName == null)
                return null;

            return FindHookMethod(td, syncVar, hookFunctionName, ref WeavingFailed);
        }

        // push hook from GetHookMethod() onto the stack as a new Action<T,T>.
        // allows for reuse without handling static/virtual cases every time.
        public void GenerateNewActionFromHookMethod(FieldDefinition syncVar, ILProcessor worker, MethodDefinition hookMethod)
        {
            // IL_000a: ldarg.0
            // IL_000b: ldftn instance void Mirror.Examples.Tanks.Tank::ExampleHook(int32, int32)
            // IL_0011: newobj instance void class [netstandard]System.Action`2<int32, int32>::.ctor(object, native int)

            // we support static hook sand instance hooks.
            if (hookMethod.IsStatic)
            {
                // for static hooks, we need to push 'null' first.
                // we can't just push nothing.
                // stack would get out of balance because we already pushed
                // other stuff above.
                worker.Emit(OpCodes.Ldnull);
            }
            else
            {
                // for instance hooks, we need to push 'this.' first.
                worker.Emit(OpCodes.Ldarg_0);
            }

            MethodReference hookMethodReference;
            // if the network behaviour class is generic, we need to make the method reference generic for correct IL
            if (hookMethod.DeclaringType.HasGenericParameters)
            {
                hookMethodReference = hookMethod.MakeHostInstanceGeneric(hookMethod.Module, hookMethod.DeclaringType.MakeGenericInstanceType(hookMethod.DeclaringType.GenericParameters.ToArray()));
            }
            else
            {
                hookMethodReference = hookMethod;
            }

            // we support regular and virtual hook functions.
            if (hookMethod.IsVirtual)
            {
                // for virtual / overwritten hooks, we need different IL.
                // this is from simply testing Action = VirtualHook; in C#.
                worker.Emit(OpCodes.Dup);
                worker.Emit(OpCodes.Ldvirtftn, hookMethodReference);
            }
            else
            {
                worker.Emit(OpCodes.Ldftn, hookMethodReference);
            }

            // call 'new Action<T,T>()' constructor to convert the function to an action
            // we need to make an instance of the generic Action<T,T>.
            //
            // TODO this allocates a new 'Action' for every SyncVar hook call.
            //      we should allocate it once and store it somewhere in the future.
            //      hooks are only called on the client though, so it's not too bad for now.
            TypeReference actionRef = assembly.MainModule.ImportReference(typeof(Action<,>));
            GenericInstanceType genericInstance = actionRef.MakeGenericInstanceType(syncVar.FieldType, syncVar.FieldType);
            worker.Emit(OpCodes.Newobj, weaverTypes.ActionT_T.MakeHostInstanceGeneric(assembly.MainModule, genericInstance));
        }

        MethodDefinition FindHookMethod(TypeDefinition td, FieldDefinition syncVar, string hookFunctionName, ref bool WeavingFailed)
        {
            List<MethodDefinition> methods = td.GetMethods(hookFunctionName);

            List<MethodDefinition> methodsWith2Param = new List<MethodDefinition>(methods.Where(m => m.Parameters.Count == 2));

            if (methodsWith2Param.Count == 0)
            {
                Log.Error($"Could not find hook for '{syncVar.Name}', hook name '{hookFunctionName}'. " +
                    $"Method signature should be {HookParameterMessage(hookFunctionName, syncVar.FieldType)}",
                    syncVar);
                WeavingFailed = true;

                return null;
            }

            foreach (MethodDefinition method in methodsWith2Param)
            {
                if (MatchesParameters(syncVar, method))
                {
                    return method;
                }
            }

            Log.Error($"Wrong type for Parameter in hook for '{syncVar.Name}', hook name '{hookFunctionName}'. " +
                     $"Method signature should be {HookParameterMessage(hookFunctionName, syncVar.FieldType)}",
                   syncVar);
            WeavingFailed = true;

            return null;
        }

        bool MatchesParameters(FieldDefinition syncVar, MethodDefinition method)
        {
            // matches void onValueChange(T oldValue, T newValue)
            return method.Parameters[0].ParameterType.FullName == syncVar.FieldType.FullName &&
                   method.Parameters[1].ParameterType.FullName == syncVar.FieldType.FullName;
        }

        public MethodDefinition GenerateSyncVarGetter(FieldDefinition fd, string originalName, FieldDefinition netFieldId)
        {
            //Create the get method
            MethodDefinition get = new MethodDefinition(
                $"get_Network{originalName}", MethodAttributes.Public |
                                              MethodAttributes.SpecialName |
                                              MethodAttributes.HideBySig,
                    fd.FieldType);

            ILProcessor worker = get.Body.GetILProcessor();

            FieldReference fr;
            if (fd.DeclaringType.HasGenericParameters)
            {
                fr = fd.MakeHostInstanceGeneric();
            }
            else
            {
                fr = fd;
            }

            FieldReference netIdFieldReference = null;
            if (netFieldId != null)
            {
                if (netFieldId.DeclaringType.HasGenericParameters)
                {
                    netIdFieldReference = netFieldId.MakeHostInstanceGeneric();
                }
                else
                {
                    netIdFieldReference = netFieldId;
                }
            }

            // [SyncVar] GameObject?
            if (fd.FieldType.Is<UnityEngine.GameObject>())
            {
                // return this.GetSyncVarGameObject(ref field, uint netId);
                // this.
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, netIdFieldReference);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, fr);
                worker.Emit(OpCodes.Call, weaverTypes.getSyncVarGameObjectReference);
                worker.Emit(OpCodes.Ret);
            }
            // [SyncVar] NetworkIdentity?
            else if (fd.FieldType.Is<NetworkIdentity>())
            {
                // return this.GetSyncVarNetworkIdentity(ref field, uint netId);
                // this.
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, netIdFieldReference);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, fr);
                worker.Emit(OpCodes.Call, weaverTypes.getSyncVarNetworkIdentityReference);
                worker.Emit(OpCodes.Ret);
            }
            else if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                // return this.GetSyncVarNetworkBehaviour<T>(ref field, uint netId);
                // this.
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, netIdFieldReference);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, fr);
                MethodReference getFunc = weaverTypes.getSyncVarNetworkBehaviourReference.MakeGeneric(assembly.MainModule, fd.FieldType);
                worker.Emit(OpCodes.Call, getFunc);
                worker.Emit(OpCodes.Ret);
            }
            // [SyncVar] int, string, etc.
            else
            {
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, fr);
                worker.Emit(OpCodes.Ret);
            }

            get.Body.Variables.Add(new VariableDefinition(fd.FieldType));
            get.Body.InitLocals = true;
            get.SemanticsAttributes = MethodSemanticsAttributes.Getter;

            return get;
        }

        // for [SyncVar] health, weaver generates
        //
        //   NetworkHealth
        //   {
        //      get => health;
        //      set => GeneratedSyncVarSetter(...)
        //   }
        //
        // the setter used to be manually IL generated, but we moved it to C# :)
        public MethodDefinition GenerateSyncVarSetter(TypeDefinition td, FieldDefinition fd, string originalName, long dirtyBit, FieldDefinition netFieldId, ref bool WeavingFailed)
        {
            //Create the set method
            MethodDefinition set = new MethodDefinition($"set_Network{originalName}", MethodAttributes.Public |
                                                                                      MethodAttributes.SpecialName |
                                                                                      MethodAttributes.HideBySig,
                    weaverTypes.Import(typeof(void)));

            ILProcessor worker = set.Body.GetILProcessor();
            FieldReference fr;
            if (fd.DeclaringType.HasGenericParameters)
            {
               fr = fd.MakeHostInstanceGeneric();
            }
            else
            {
                fr = fd;
            }

            FieldReference netIdFieldReference = null;
            if (netFieldId != null)
            {
                if (netFieldId.DeclaringType.HasGenericParameters)
                {
                    netIdFieldReference = netFieldId.MakeHostInstanceGeneric();
                }
                else
                {
                  netIdFieldReference = netFieldId;
                }
            }

            // if (!SyncVarEqual(value, ref playerData))
            Instruction endOfMethod = worker.Create(OpCodes.Nop);

            // NOTE: SyncVar...Equal functions are static.
            // don't Emit Ldarg_0 aka 'this'.

            // call WeaverSyncVarSetter<T>(T value, ref T field, ulong dirtyBit, Action<T, T> OnChanged = null)
            //   IL_0000: ldarg.0
            //   IL_0001: ldarg.1
            //   IL_0002: ldarg.0
            //   IL_0003: ldflda int32 Mirror.Examples.Tanks.Tank::health
            //   IL_0008: ldc.i4.1
            //   IL_0009: conv.i8
            //   IL_000a: ldnull
            //   IL_000b: call instance void [Mirror]Mirror.NetworkBehaviour::GeneratedSyncVarSetter<int32>(!!0, !!0&, uint64, class [netstandard]System.Action`2<!!0, !!0>)
            //   IL_0010: ret

            // 'this.' for the call
            worker.Emit(OpCodes.Ldarg_0);

            // first push 'value'
            worker.Emit(OpCodes.Ldarg_1);

            // push 'ref T this.field'
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldflda, fr);

            // push the dirty bit for this SyncVar
            worker.Emit(OpCodes.Ldc_I8, dirtyBit);

            // hook? then push 'new Action<T,T>(Hook)' onto stack
            MethodDefinition hookMethod = GetHookMethod(td, fd, ref WeavingFailed);
            if (hookMethod != null)
            {
                GenerateNewActionFromHookMethod(fd, worker, hookMethod);
            }
            // otherwise push 'null' as hook
            else
            {
                worker.Emit(OpCodes.Ldnull);
            }

            // call GeneratedSyncVarSetter<T>.
            // special cases for GameObject/NetworkIdentity/NetworkBehaviour
            // passing netId too for persistence.
            if (fd.FieldType.Is<UnityEngine.GameObject>())
            {
                // GameObject setter needs one more parameter: netId field ref
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netIdFieldReference);
                worker.Emit(OpCodes.Call, weaverTypes.generatedSyncVarSetter_GameObject);
            }
            else if (fd.FieldType.Is<NetworkIdentity>())
            {
                // NetworkIdentity setter needs one more parameter: netId field ref
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netIdFieldReference);
                worker.Emit(OpCodes.Call, weaverTypes.generatedSyncVarSetter_NetworkIdentity);
            }
            // TODO this only uses the persistent netId for types DERIVED FROM NB.
            //      not if the type is just 'NetworkBehaviour'.
            //      this is what original implementation did too. fix it after.
            else if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                // NetworkIdentity setter needs one more parameter: netId field ref
                // (actually its a NetworkBehaviourSyncVar type)
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netIdFieldReference);
                // make generic version of GeneratedSyncVarSetter_NetworkBehaviour<T>
                MethodReference getFunc = weaverTypes.generatedSyncVarSetter_NetworkBehaviour_T.MakeGeneric(assembly.MainModule, fd.FieldType);
                worker.Emit(OpCodes.Call, getFunc);
            }
            else
            {
                // make generic version of GeneratedSyncVarSetter<T>
                MethodReference generic = weaverTypes.generatedSyncVarSetter.MakeGeneric(assembly.MainModule, fd.FieldType);
                worker.Emit(OpCodes.Call, generic);
            }

            worker.Append(endOfMethod);

            worker.Emit(OpCodes.Ret);

            set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.In, fd.FieldType));
            set.SemanticsAttributes = MethodSemanticsAttributes.Setter;

            return set;
        }

        public void ProcessSyncVar(TypeDefinition td, FieldDefinition fd, Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds, long dirtyBit, ref bool WeavingFailed)
        {
            string originalName = fd.Name;

            // GameObject/NetworkIdentity SyncVars have a new field for netId
            FieldDefinition netIdField = null;
            // NetworkBehaviour has different field type than other NetworkIdentityFields
            if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                netIdField = new FieldDefinition($"___{fd.Name}NetId",
                   FieldAttributes.Family, // needs to be protected for generic classes, otherwise access isn't allowed
                   weaverTypes.Import<NetworkBehaviourSyncVar>());
                netIdField.DeclaringType = td;

                syncVarNetIds[fd] = netIdField;
            }
            else if (fd.FieldType.IsNetworkIdentityField())
            {
                netIdField = new FieldDefinition($"___{fd.Name}NetId",
                    FieldAttributes.Family, // needs to be protected for generic classes, otherwise access isn't allowed
                    weaverTypes.Import<uint>());
                netIdField.DeclaringType = td;

                syncVarNetIds[fd] = netIdField;
            }

            MethodDefinition get = GenerateSyncVarGetter(fd, originalName, netIdField);
            MethodDefinition set = GenerateSyncVarSetter(td, fd, originalName, dirtyBit, netIdField, ref WeavingFailed);

            //NOTE: is property even needed? Could just use a setter function?
            //create the property
            PropertyDefinition propertyDefinition = new PropertyDefinition($"Network{originalName}", PropertyAttributes.None, fd.FieldType)
            {
                GetMethod = get,
                SetMethod = set
            };

            //add the methods and property to the type.
            td.Methods.Add(get);
            td.Methods.Add(set);
            td.Properties.Add(propertyDefinition);
            syncVarAccessLists.replacementSetterProperties[fd] = set;

            // replace getter field if GameObject/NetworkIdentity so it uses
            // netId instead
            // -> only for GameObjects, otherwise an int syncvar's getter would
            //    end up in recursion.
            if (fd.FieldType.IsNetworkIdentityField())
            {
                syncVarAccessLists.replacementGetterProperties[fd] = get;
            }
        }

        public (List<FieldDefinition> syncVars, Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds) ProcessSyncVars(TypeDefinition td, ref bool WeavingFailed)
        {
            List<FieldDefinition> syncVars = new List<FieldDefinition>();
            Dictionary<FieldDefinition, FieldDefinition> syncVarNetIds = new Dictionary<FieldDefinition, FieldDefinition>();

            // the mapping of dirtybits to sync-vars is implicit in the order of the fields here. this order is recorded in m_replacementProperties.
            // start assigning syncvars at the place the base class stopped, if any
            int dirtyBitCounter = syncVarAccessLists.GetSyncVarStart(td.BaseType.FullName);

            // find syncvars
            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.HasCustomAttribute<SyncVarAttribute>())
                {
                    if ((fd.Attributes & FieldAttributes.Static) != 0)
                    {
                        Log.Error($"{fd.Name} cannot be static", fd);
                        WeavingFailed = true;
                        continue;
                    }

                    if (fd.FieldType.IsGenericParameter)
                    {
                        Log.Error($"{fd.Name} has generic type. Generic SyncVars are not supported", fd);
                        WeavingFailed = true;
                        continue;
                    }

                    if (fd.FieldType.IsArray)
                    {
                        Log.Error($"{fd.Name} has invalid type. Use SyncLists instead of arrays", fd);
                        WeavingFailed = true;
                        continue;
                    }

                    if (SyncObjectInitializer.ImplementsSyncObject(fd.FieldType))
                    {
                        Log.Warning($"{fd.Name} has [SyncVar] attribute. SyncLists should not be marked with SyncVar", fd);
                    }
                    else
                    {
                        syncVars.Add(fd);

                        ProcessSyncVar(td, fd, syncVarNetIds, 1L << dirtyBitCounter, ref WeavingFailed);
                        dirtyBitCounter += 1;

                        if (dirtyBitCounter > SyncVarLimit)
                        {
                            Log.Error($"{td.Name} has > {SyncVarLimit} SyncVars. Consider refactoring your class into multiple components", td);
                            WeavingFailed = true;
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
            syncVarAccessLists.SetNumSyncVars(td.FullName, syncVars.Count);

            return (syncVars, syncVarNetIds);
        }
    }
}
