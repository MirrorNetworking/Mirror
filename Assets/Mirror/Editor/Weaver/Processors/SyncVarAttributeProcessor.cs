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
        AssemblyDefinition assembly;
        WeaverTypes weaverTypes;
        SyncVarAccessLists syncVarAccessLists;
        Logger Log;

        // keep original name for now. less confusing in IL.
        public const string OriginalSyncVarSuffix = "";

        // add suffix to SyncVar<T>. SyncVarDrawer will exclude it.
        public const string NewSyncVarTSuffix = "_generated";

        static string HookParameterMessage(string hookName, TypeReference ValueType) =>
            $"void {hookName}({ValueType} oldValue, {ValueType} newValue)";

        public SyncVarAttributeProcessor(AssemblyDefinition assembly, WeaverTypes weaverTypes, SyncVarAccessLists syncVarAccessLists, Logger Log)
        {
            this.assembly = assembly;
            this.weaverTypes = weaverTypes;
            this.syncVarAccessLists = syncVarAccessLists;
            this.Log = Log;
        }

        // Get hook method if any
        public static MethodDefinition GetHookMethod(TypeDefinition td, FieldDefinition syncVar, Logger Log, ref bool WeavingFailed)
        {
            CustomAttribute syncVarAttr = syncVar.GetCustomAttribute<SyncVarAttribute>();

            if (syncVarAttr == null)
                return null;

            string hookFunctionName = syncVarAttr.GetField<string>("hook", null);

            if (hookFunctionName == null)
                return null;

            return FindHookMethod(td, syncVar, hookFunctionName, Log, ref WeavingFailed);
        }

        static MethodDefinition FindHookMethod(TypeDefinition td, FieldDefinition syncVar, string hookFunctionName, Logger Log, ref bool WeavingFailed)
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

        static bool MatchesParameters(FieldDefinition syncVar, MethodDefinition method)
        {
            // matches void onValueChange(T oldValue, T newValue)
            return method.Parameters[0].ParameterType.FullName == syncVar.FieldType.FullName &&
                   method.Parameters[1].ParameterType.FullName == syncVar.FieldType.FullName;
        }

        public MethodDefinition GenerateSyncVarGetter(FieldDefinition syncVarT, TypeReference syncVarT_ForValue, FieldDefinition originalSyncVar, string originalName)
        {
            //Create the get method
            MethodDefinition get = new MethodDefinition(
                $"get_Network{originalName}", MethodAttributes.Public |
                                              MethodAttributes.SpecialName |
                                              MethodAttributes.HideBySig,
                    originalSyncVar.FieldType);

            ILProcessor worker = get.Body.GetILProcessor();

            // make generic instance for SyncVar<T>.Value getter
            // so we have SyncVar<int>.Value etc.
            MethodReference syncVarT_Value_Get_ForValue = GetSyncVarT_Value_Getter(originalSyncVar, syncVarT_ForValue, weaverTypes);

            // push this.SyncVar<T>.Value on stack
            // when doing it manually, this is the generated IL:
            //   IL_0001: ldfld class [Mirror]Mirror.SyncVar`1<int32> Mirror.Examples.Tanks.Test::exampleT
            //   IL_0006: callvirt instance !0 class [Mirror]Mirror.SyncVar`1<int32>::get_Value()
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldfld, syncVarT);
            worker.Emit(OpCodes.Callvirt, syncVarT_Value_Get_ForValue);
            worker.Emit(OpCodes.Ret);

            get.Body.Variables.Add(new VariableDefinition(originalSyncVar.FieldType));
            get.Body.InitLocals = true;
            get.SemanticsAttributes = MethodSemanticsAttributes.Getter;

            return get;
        }

        public MethodDefinition GenerateSyncVarSetter(FieldDefinition syncVarT, TypeReference syncVarT_ForValue, FieldDefinition originalSyncVar, string originalName, ref bool WeavingFailed)
        {
            //Create the set method
            MethodDefinition set = new MethodDefinition($"set_Network{originalName}", MethodAttributes.Public |
                                                                                      MethodAttributes.SpecialName |
                                                                                      MethodAttributes.HideBySig,
                    weaverTypes.Import(typeof(void)));

            ILProcessor worker = set.Body.GetILProcessor();

            // make generic instance for SyncVar<T>.Value setter
            // so we have SyncVar<int>.Value etc.
            MethodReference syncVarT_Value_Set_ForValue = GetSyncVarT_Value_Setter(originalSyncVar, syncVarT_ForValue, weaverTypes);

            // when doing this.SyncVar<T>.Value = ... manually, this is the
            // generated IL:
            //   IL_0000: ldarg.0
            //   IL_0001: ldfld class [Mirror]Mirror.SyncVar`1<int32> Mirror.Examples.Tanks.Test::exampleT
            //   IL_0006: ldarg.1
            //   IL_0007: callvirt instance void class [Mirror]Mirror.SyncVar`1<int32>::set_Value(!0)
            worker.Emit(OpCodes.Ldarg_0); // 'this.'
            worker.Emit(OpCodes.Ldfld, syncVarT);
            worker.Emit(OpCodes.Ldarg_1); // 'value' from setter
            worker.Emit(OpCodes.Callvirt, syncVarT_Value_Set_ForValue);

            worker.Emit(OpCodes.Ret);

            set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.In, originalSyncVar.FieldType));
            set.SemanticsAttributes = MethodSemanticsAttributes.Setter;

            return set;
        }

        // make SyncVar<T> instance for given type.
        // with explicit replacements for SyncVarGameObject/NetworkIdentity/NetworkBehaviour
        // for persistence through netId
        public void CreateSyncVarT_Field(FieldDefinition originalField, WeaverTypes weaverTypes, out TypeReference typeReference, out FieldDefinition fieldDefinition)
        {
            // Weaver will show a warning that SyncObjects need to be 'readonly'
            // so let's make it 'readonly'.

            // copy original field's visibility.
            // if original [SyncVar] is private, SyncVar<T> should be too.
            // otherwise it would be shown in inspector.
            // TODO Unity Inspector doesn't show 'readonly' fields in Inspector (#1368395)
            // make it readonly again later.
            //FieldAttributes fieldAttributes = originalField.Attributes | FieldAttributes.InitOnly;
            FieldAttributes fieldAttributes = originalField.Attributes;

            if (originalField.FieldType.Is<UnityEngine.GameObject>())
            {
                typeReference = weaverTypes.SyncVarT_GameObject_Type;
                fieldDefinition = new FieldDefinition(originalField.Name + NewSyncVarTSuffix, fieldAttributes, typeReference);
            }
            else if (originalField.FieldType.Is<NetworkIdentity>())
            {
                typeReference = weaverTypes.SyncVarT_NetworkIdentity_Type;
                fieldDefinition = new FieldDefinition(originalField.Name + NewSyncVarTSuffix, fieldAttributes, typeReference);
            }
            // SyncVarNetworkBehaviour<T> with explicit type for
            // OnHook(Monster, Monster) instead of OnHook(NetworkBehaviour, NetworkBehaviour)
            else if (originalField.FieldType.Is<NetworkBehaviour>() ||
                     originalField.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                typeReference = weaverTypes.SyncVarT_NetworkBehaviour_Type.MakeGenericInstanceType(originalField.FieldType);
                fieldDefinition = new FieldDefinition(originalField.Name + NewSyncVarTSuffix, fieldAttributes, typeReference);
            }
            // SyncVar<T>
            else
            {
                // make a generic instance for SyncVar<originalField Type>
                typeReference = weaverTypes.SyncVarT_Type.MakeGenericInstanceType(originalField.FieldType);
                fieldDefinition = new FieldDefinition(originalField.Name + NewSyncVarTSuffix, fieldAttributes, typeReference);
            }
        }

        // get SyncVar<T>.Value getter for given type.
        // with explicit replacements for SyncVarGameObject/NetworkIdentity/NetworkBehaviour
        // for persistence through netId
        public MethodReference GetSyncVarT_Value_Getter(FieldDefinition originalField, TypeReference syncVarT_ForValue, WeaverTypes weaverTypes)
        {
            if (originalField.FieldType.Is<UnityEngine.GameObject>())
            {
                return weaverTypes.SyncVarT_GameObject_Value_Get_Reference;
            }
            else if (originalField.FieldType.Is<NetworkIdentity>())
            {
                return weaverTypes.SyncVarT_NetworkIdentity_Value_Get_Reference;
            }
            // SyncVarNetworkBehaviour<T> with explicit type for
            // OnHook(Monster, Monster) instead of OnHook(NetworkBehaviour, NetworkBehaviour)
            else if (originalField.FieldType.Is<NetworkBehaviour>() ||
                     originalField.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                // make a generic instance for SyncVarNetworkBehaviour<originalField Type>.Value.get
                GenericInstanceType syncVarNetworkBehaviour_Value_GenericInstanceType = (GenericInstanceType)syncVarT_ForValue;
                return weaverTypes.SyncVarT_NetworkBehaviour_Value_Get_Reference.MakeHostInstanceGeneric(assembly.MainModule, syncVarNetworkBehaviour_Value_GenericInstanceType);
            }
            // SyncVar<T>
            else
            {
                // make a generic instance for SyncVar<originalField Type>.Value.get
                GenericInstanceType syncVarT_Value_GenericInstanceType = (GenericInstanceType)syncVarT_ForValue;
                return weaverTypes.SyncVarT_Value_Get_Reference.MakeHostInstanceGeneric(assembly.MainModule, syncVarT_Value_GenericInstanceType);
            }
        }

        // get SyncVar<T>.Value setter for given type.
        // with explicit replacements for SyncVarGameObject/NetworkIdentity/NetworkBehaviour
        // for persistence through netId
        public MethodReference GetSyncVarT_Value_Setter(FieldDefinition originalField, TypeReference syncVarT_ForValue, WeaverTypes weaverTypes)
        {
            if (originalField.FieldType.Is<UnityEngine.GameObject>())
            {
                return weaverTypes.SyncVarT_GameObject_Value_Set_Reference;
            }
            else if (originalField.FieldType.Is<NetworkIdentity>())
            {
                return weaverTypes.SyncVarT_NetworkIdentity_Value_Set_Reference;
            }
            // SyncVarNetworkBehaviour<T> with explicit type for
            // OnHook(Monster, Monster) instead of OnHook(NetworkBehaviour, NetworkBehaviour)
            else if (originalField.FieldType.Is<NetworkBehaviour>() ||
                     originalField.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                // make a generic instance for SyncVarNetworkBehaviour<originalField Type>.Value.get
                GenericInstanceType syncVarNetworkBehaviour_Value_GenericInstanceType = (GenericInstanceType)syncVarT_ForValue;
                return weaverTypes.SyncVarT_NetworkBehaviour_Value_Set_Reference.MakeHostInstanceGeneric(assembly.MainModule, syncVarNetworkBehaviour_Value_GenericInstanceType);
            }
            // SyncVar<T>
            else
            {
                // make a generic instance for SyncVar<originalField Type>.Value.get
                GenericInstanceType syncVarT_Value_GenericInstanceType = (GenericInstanceType)syncVarT_ForValue;
                return weaverTypes.SyncVarT_Value_Set_Reference.MakeHostInstanceGeneric(assembly.MainModule, syncVarT_Value_GenericInstanceType);
            }
        }

        // get SyncVar<T> ctor for given type.
        // with explicit replacements for SyncVarGameObject/NetworkIdentity/NetworkBehaviour
        // for persistence through netId
        public static MethodReference GetSyncVarT_Ctor(FieldDefinition originalField, ModuleDefinition module, WeaverTypes weaverTypes)
        {
            if (originalField.FieldType.Is<UnityEngine.GameObject>())
            {
                return weaverTypes.SyncVarT_GameObject_Constructor;
            }
            else if (originalField.FieldType.Is<NetworkIdentity>())
            {
                return weaverTypes.SyncVarT_NetworkIdentity_Constructor;
            }
            // SyncVarNetworkBehaviour<T> with explicit type for
            // OnHook(Monster, Monster) instead of OnHook(NetworkBehaviour, NetworkBehaviour)
            else if (originalField.FieldType.Is<NetworkBehaviour>() ||
                     originalField.FieldType.IsDerivedFrom<NetworkBehaviour>())
            {
                // make generic instance of SyncVarNetworkBehaviour<T> type for the type of 'value'
                TypeReference syncVarNetworkBehaviour_ForValue = weaverTypes.SyncVarT_NetworkBehaviour_Type.MakeGenericInstanceType(originalField.FieldType);

                // make generic ctor for SyncVar<T> for the target type SyncVar<T> with type of 'value'
                GenericInstanceType syncVarT_GenericInstanceType = (GenericInstanceType)syncVarNetworkBehaviour_ForValue;
                return weaverTypes.SyncVarT_NetworkBehaviour_Constructor.MakeHostInstanceGeneric(module, syncVarT_GenericInstanceType);
            }
            // SyncVar<T>
            else
            {
                // make generic instance of SyncVar<T> type for the type of 'value'
                TypeReference syncVarT_ForValue = weaverTypes.SyncVarT_Type.MakeGenericInstanceType(originalField.FieldType);

                // make generic ctor for SyncVar<T> for the target type SyncVar<T> with type of 'value'
                GenericInstanceType syncVarT_GenericInstanceType = (GenericInstanceType)syncVarT_ForValue;
                return weaverTypes.SyncVarT_GenericConstructor.MakeHostInstanceGeneric(module, syncVarT_GenericInstanceType);
            }
        }

        // ProcessSyncVar is called while iterating td.Fields.
        // can't add to it while iterating.
        // new SyncVar<T> fields are added to 'addedSyncVarTs' with
        //   <SyncVar<T>, [SyncVar] original>
        public void ProcessSyncVar(TypeDefinition td, FieldDefinition fd, Dictionary<FieldDefinition, FieldDefinition> addedSyncVarTs, ref bool WeavingFailed)
        {
            string originalName = fd.Name;

            // IMPORTANT: if a SyncVar<T> gives a weaver error:
            // '... was defined in another module and needs to be imported'
            // then the caller didn't added it to td.Fields!

            // make generic instance of SyncVar<T> type for the type of 'value'
            // initial value is set in constructor.
            CreateSyncVarT_Field(fd, weaverTypes, out TypeReference syncVarT_ForValue, out FieldDefinition syncVarTField);
            addedSyncVarTs[syncVarTField] = fd;

            // add getters/setters so that SyncVarAttributeAccessReplacer can
            // simply change the instruction to 'Call' with getter/setter.
            MethodDefinition get = GenerateSyncVarGetter(syncVarTField, syncVarT_ForValue, fd, originalName);
            MethodDefinition set = GenerateSyncVarSetter(syncVarTField, syncVarT_ForValue, fd, originalName, ref WeavingFailed);

            //NOTE: is property even needed? Could just use a setter function?
            //create the property
            PropertyDefinition propertyDefinition = new PropertyDefinition($"{originalName}{NewSyncVarTSuffix}_property", PropertyAttributes.None, fd.FieldType)
            {
                GetMethod = get,
                SetMethod = set
            };

            //add the methods and property to the type.
            td.Methods.Add(get);
            td.Methods.Add(set);
            td.Properties.Add(propertyDefinition);

            // add getter/setter to replacement lists
            syncVarAccessLists.replacementSetterProperties[fd] = set;
            syncVarAccessLists.replacementGetterProperties[fd] = get;

            // removing the original [SyncVar] still gives us an error about
            // '... was defined in another module and needs to be imported'.
            // seems like Weaver still uses it elsewehre afterwards.
            // let's simply rename it and hide it from the inspector.
            fd.Name += OriginalSyncVarSuffix;

            // don't show old [SyncVar] and generated SyncVar<T> in Inspector:
            // => can't remove orignal [SyncVar] because we still replace access
            //    to it with the generated properties.
            // => simply add [HideInInspector] instead.
            // TODO try to fully remove it again later with ProcessSyncVars()
            //      via addedSyncVarTs
            MethodDefinition ctor = weaverTypes.hideInInspectorAttribute.GetConstructors().First();
            CustomAttribute hideInInspector = new CustomAttribute(assembly.MainModule.ImportReference(ctor));
            fd.CustomAttributes.Add(hideInInspector);
        }

        public void ProcessSyncVars(TypeDefinition td, Dictionary<FieldDefinition, FieldDefinition> addedSyncVarTs, ref bool WeavingFailed)
        {
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
                        ProcessSyncVar(td, fd, addedSyncVarTs, ref WeavingFailed);
                    }
                }
            }

            // add all added SyncVar<T>s
            foreach (FieldDefinition fd in addedSyncVarTs.Keys)
            {
                td.Fields.Add(fd);
            }
        }

        // inject initialization code for SyncVar<T> from [SyncVar] into ctor
        // called from NetworkBehaviourProcessor.InjectIntoInstanceConstructor()
        // see also: https://groups.google.com/g/mono-cecil/c/JCLRPxOym4A?pli=1
        public static void InjectSyncVarT_Initialization(AssemblyDefinition assembly, ILProcessor ctorWorker, TypeDefinition td, FieldDefinition syncVarT, FieldDefinition originalSyncVar, WeaverTypes weaverTypes, Logger Log, ref bool WeavingFailed)
        {
            // find hook method in original [SyncVar(hook="func")] attribute (if any)
            MethodDefinition hookMethod = GetHookMethod(td, originalSyncVar, Log, ref WeavingFailed);

            // final 'StFld syncVarT' needs 'this.' in front
            ctorWorker.Emit(OpCodes.Ldarg_0);

            // push 'new SyncVar<T>(value, hook)' on stack
            ctorWorker.Emit(OpCodes.Ldarg_0);                // 'this' for this.originalSyncVar
            ctorWorker.Emit(OpCodes.Ldfld, originalSyncVar); // value = originalSyncVar
            // pass hook parameter (a method converted to an Action)
            if (hookMethod != null)
            {
                // 'ldftn' loads the hook function onto the stack.
                // for static hooks, we need to push 'null' onto stack first.
                // for instance hooks, we need to push 'this' onto stack first.
                // (from C# generated IL code)
                ctorWorker.Emit(hookMethod.IsStatic ? OpCodes.Ldnull : OpCodes.Ldarg_0);

                // when doing SyncVar<T> test = new SyncVar<T>(value, hook),
                // this is the IL code to convert hook to Action:
                //   ldftn instance void Mirror.Examples.Tanks.Test::OnChanged(int32, int32)
                //   newobj instance void class [netstandard]System.Action`2<int32, int32>::.ctor(object, native int)
                if (hookMethod.IsVirtual)
                {
                    // Ldvirtftn needs one additional parameter: the object
                    // C# compiler seems to simply use 'dup' for the previous
                    // Ldarg_0
                    ctorWorker.Emit(OpCodes.Dup);
                    ctorWorker.Emit(OpCodes.Ldvirtftn, hookMethod);
                }
                else
                {
                    ctorWorker.Emit(OpCodes.Ldftn, hookMethod);
                }

                // make generic instance of Action<T,T> type for the type of 'value'
                TypeReference actionT_T_ForValue = weaverTypes.ActionT_T_Type.MakeGenericInstanceType(originalSyncVar.FieldType, originalSyncVar.FieldType);

                // make generic ctor for Action<T,T> for the target type Action<T,T> with type of 'value'
                GenericInstanceType actionT_T_GenericInstanceType = (GenericInstanceType)actionT_T_ForValue;
                MethodReference actionT_T_Ctor_ForValue = weaverTypes.ActionT_T_GenericConstructor.MakeHostInstanceGeneric(assembly.MainModule, actionT_T_GenericInstanceType);
                ctorWorker.Emit(OpCodes.Newobj, actionT_T_Ctor_ForValue);
            }
            else
            {
                // push 'hook = null' onto stack
                ctorWorker.Emit(OpCodes.Ldnull);
            }

            // make generic ctor for SyncVar<T> for the target type SyncVar<T> with type of 'value'
            MethodReference syncVarT_Ctor_ForValue = GetSyncVarT_Ctor(originalSyncVar, assembly.MainModule, weaverTypes);
            ctorWorker.Emit(OpCodes.Newobj, syncVarT_Ctor_ForValue);

            // store result in SyncVar<T> member
            ctorWorker.Emit(OpCodes.Stfld, syncVarT);
        }
    }
}
