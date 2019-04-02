// all the [SyncVar] code from NetworkBehaviourProcessor in one place
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    public static class SyncVarProcessor
    {
        const int SyncVarLimit = 64; // ulong = 64 bytes

        // returns false for error, not for no-hook-exists
        public static bool CheckForHookFunction(TypeDefinition td, FieldDefinition syncVar, out MethodDefinition foundMethod)
        {
            foundMethod = null;
            foreach (var ca in syncVar.CustomAttributes)
            {
                if (ca.AttributeType.FullName == Weaver.SyncVarType.FullName)
                {
                    foreach (CustomAttributeNamedArgument customField in ca.Fields)
                    {
                        if (customField.Name == "hook")
                        {
                            string hookFunctionName = customField.Argument.Value as string;

                            foreach (var m in td.Methods)
                            {
                                if (m.Name == hookFunctionName)
                                {
                                    if (m.Parameters.Count == 1)
                                    {
                                        if (m.Parameters[0].ParameterType != syncVar.FieldType)
                                        {
                                            Weaver.Error("SyncVar Hook function " + hookFunctionName + " has wrong type signature for " + td.Name);
                                            return false;
                                        }
                                        foundMethod = m;
                                        return true;
                                    }
                                    Weaver.Error("SyncVar Hook function " + hookFunctionName + " must have one argument " + td.Name);
                                    return false;
                                }
                            }
                            Weaver.Error("SyncVar Hook function " + hookFunctionName + " not found for " + td.Name);
                            return false;
                        }
                    }
                }
            }
            return true;
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
                getWorker.Append(getWorker.Create(OpCodes.Ldarg_0)); // this.
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
                getWorker.Append(getWorker.Create(OpCodes.Ldarg_0)); // this.
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

            CheckForHookFunction(td, fd, out MethodDefinition hookFunctionMethod);

            if (hookFunctionMethod != null)
            {
                //if (NetworkServer.localClientActive && !syncVarHookGuard)
                Instruction label = setWorker.Create(OpCodes.Nop);
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.NetworkServerGetLocalClientActive));
                setWorker.Append(setWorker.Create(OpCodes.Brfalse, label));
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.getSyncVarHookGuard));
                setWorker.Append(setWorker.Create(OpCodes.Brtrue, label));

                // syncVarHookGuard = true;
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldc_I4_1));
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.setSyncVarHookGuard));

                // call hook
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_1));
                setWorker.Append(setWorker.Create(OpCodes.Call, hookFunctionMethod));

                // syncVarHookGuard = false;
                setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
                setWorker.Append(setWorker.Create(OpCodes.Ldc_I4_0));
                setWorker.Append(setWorker.Create(OpCodes.Call, Weaver.setSyncVarHookGuard));

                setWorker.Append(label);
            }

            // this
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));

            // new value to set
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_1));

            // reference to field to set
            setWorker.Append(setWorker.Create(OpCodes.Ldarg_0));
            setWorker.Append(setWorker.Create(OpCodes.Ldflda, fd));

            // dirty bit
            setWorker.Append(setWorker.Create(OpCodes.Ldc_I8, dirtyBit)); // 8 byte integer aka long


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

            var get = ProcessSyncVarGet(fd, originalName, netIdField);
            var set = ProcessSyncVarSet(td, fd, originalName, dirtyBit, netIdField);

            //NOTE: is property even needed? Could just use a setter function?
            //create the property
            PropertyDefinition propertyDefinition = new PropertyDefinition("Network" + originalName, PropertyAttributes.None, fd.FieldType)
            {
                GetMethod = get, SetMethod = set
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
                foreach (var ca in fd.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == Weaver.SyncVarType.FullName)
                    {
                        var resolvedField = fd.FieldType.Resolve();

                        if (resolvedField.IsDerivedFrom(Weaver.NetworkBehaviourType))
                        {
                            Weaver.Error("SyncVar [" + fd.FullName + "] cannot be derived from NetworkBehaviour.");
                            return;
                        }

                        if (resolvedField.IsDerivedFrom(Weaver.ScriptableObjectType))
                        {
                            Weaver.Error("SyncVar [" + fd.FullName + "] cannot be derived from ScriptableObject.");
                            return;
                        }

                        if ((fd.Attributes & FieldAttributes.Static) != 0)
                        {
                            Weaver.Error("SyncVar [" + fd.FullName + "] cannot be static.");
                            return;
                        }

                        if (resolvedField.HasGenericParameters)
                        {
                            Weaver.Error("SyncVar [" + fd.FullName + "] cannot have generic parameters.");
                            return;
                        }

                        if (resolvedField.IsInterface)
                        {
                            Weaver.Error("SyncVar [" + fd.FullName + "] cannot be an interface.");
                            return;
                        }

                        var fieldModuleName = resolvedField.Module.Name;
                        if (fieldModuleName != Weaver.CurrentAssembly.MainModule.Name &&
                            fieldModuleName != Weaver.UnityAssembly.MainModule.Name &&
                            fieldModuleName != Weaver.NetAssembly.MainModule.Name &&
                            fieldModuleName != Weaver.CorLibModule.Name &&
                            fieldModuleName != "System.Runtime.dll" && // this is only for Metro, built-in types are not in corlib on metro
                            fieldModuleName != "netstandard.dll" // handle built-in types when weaving new C#7 compiler assemblies
                            )
                        {
                            Weaver.Error("SyncVar [" + fd.FullName + "] from " + resolvedField.Module.ToString() + " cannot be a different module.");
                            return;
                        }

                        if (fd.FieldType.IsArray)
                        {
                            Weaver.Error("SyncVar [" + fd.FullName + "] cannot be an array. Use a SyncList instead.");
                            return;
                        }

                        if (SyncObjectInitializer.ImplementsSyncObject(fd.FieldType))
                        {
                            Log.Warning(string.Format("Script class [{0}] has [SyncVar] attribute on SyncList field {1}, SyncLists should not be marked with SyncVar.", td.FullName, fd.Name));
                            break;
                        }

                        syncVars.Add(fd);

                        ProcessSyncVar(td, fd, syncVarNetIds, 1L << dirtyBitCounter);
                        dirtyBitCounter += 1;
                        numSyncVars += 1;

                        if (dirtyBitCounter == SyncVarLimit)
                        {
                            Weaver.Error("Script class [" + td.FullName + "] has too many SyncVars (" + SyncVarLimit + "). (This could include base classes)");
                            return;
                        }
                        break;
                    }
                }

                if (fd.FieldType.FullName.Contains("Mirror.SyncListStruct"))
                {
                    Weaver.Error("SyncListStruct member variable [" + fd.FullName + "] must use a dervied class, like \"class MySyncList : SyncListStruct<MyStruct> {}\".");
                    return;
                }

                if (fd.FieldType.Resolve().ImplementsInterface(Weaver.SyncObjectType))
                {
                    if (fd.IsStatic)
                    {
                        Weaver.Error("SyncList [" + td.FullName + ":" + fd.FullName + "] cannot be a static");
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
    }
}
