using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    /// <summary>
    /// Processes SyncEvents in NetworkBehaviour
    /// </summary>
    public static class SyncEventProcessor
    {
        public static MethodDefinition ProcessEventInvoke(TypeDefinition td, EventDefinition ed)
        {
            // find the field that matches the event
            FieldDefinition eventField = null;
            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.FullName == ed.FullName)
                {
                    eventField = fd;
                    break;
                }
            }
            if (eventField == null)
            {
                Weaver.Error($"event field not found for {ed.Name}. Did you declare it as an event?", ed);
                return null;
            }

            MethodDefinition cmd = new MethodDefinition(Weaver.InvokeRpcPrefix + ed.Name, MethodAttributes.Family |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    WeaverTypes.voidType);

            ILProcessor worker = cmd.Body.GetILProcessor();
            Instruction label1 = worker.Create(OpCodes.Nop);
            Instruction label2 = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteClientActiveCheck(worker, ed.Name, label1, "Event");

            // null event check
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Castclass, td));
            worker.Append(worker.Create(OpCodes.Ldfld, eventField));
            worker.Append(worker.Create(OpCodes.Brtrue, label2));
            worker.Append(worker.Create(OpCodes.Ret));
            worker.Append(label2);

            // setup reader
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Castclass, td));
            worker.Append(worker.Create(OpCodes.Ldfld, eventField));

            // read the event arguments
            MethodReference invoke = Resolvers.ResolveMethod(eventField.FieldType, Weaver.CurrentAssembly, "Invoke");
            if (!NetworkBehaviourProcessor.ReadArguments(invoke.Resolve(), worker, RemoteCallType.SyncEvent))
                return null;

            // invoke actual event delegate function
            worker.Append(worker.Create(OpCodes.Callvirt, invoke));
            worker.Append(worker.Create(OpCodes.Ret));

            NetworkBehaviourProcessor.AddInvokeParameters(cmd.Parameters);

            return cmd;
        }

        public static MethodDefinition ProcessEventCall(TypeDefinition td, EventDefinition ed, CustomAttribute syncEventAttr)
        {
            MethodReference invoke = Resolvers.ResolveMethod(ed.EventType, Weaver.CurrentAssembly, "Invoke");
            MethodDefinition evt = new MethodDefinition(Weaver.SyncEventPrefix + ed.Name, MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    WeaverTypes.voidType);
            // add paramters
            foreach (ParameterDefinition pd in invoke.Parameters)
            {
                evt.Parameters.Add(new ParameterDefinition(pd.Name, ParameterAttributes.None, pd.ParameterType));
            }

            ILProcessor worker = evt.Body.GetILProcessor();
            Instruction label = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteSetupLocals(worker);

            NetworkBehaviourProcessor.WriteServerActiveCheck(worker, ed.Name, label, "Event");

            NetworkBehaviourProcessor.WriteCreateWriter(worker);

            // write all the arguments that the user passed to the syncevent
            if (!NetworkBehaviourProcessor.WriteArguments(worker, invoke.Resolve(), RemoteCallType.SyncEvent))
                return null;

            // invoke interal send and return
            // this
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldtoken, td));
            // invokerClass
            worker.Append(worker.Create(OpCodes.Call, WeaverTypes.getTypeFromHandleReference));
            worker.Append(worker.Create(OpCodes.Ldstr, ed.Name));
            // writer
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4, syncEventAttr.GetField("channel", 0)));
            worker.Append(worker.Create(OpCodes.Call, WeaverTypes.sendEventInternal));

            NetworkBehaviourProcessor.WriteRecycleWriter(worker);

            worker.Append(worker.Create(OpCodes.Ret));

            return evt;
        }

        public static void ProcessEvents(TypeDefinition td, List<EventDefinition> events, List<MethodDefinition> eventInvocationFuncs)
        {
            // find events
            foreach (EventDefinition ed in td.Events)
            {
                CustomAttribute syncEventAttr = ed.GetCustomAttribute(WeaverTypes.SyncEventType.FullName);

                if (syncEventAttr != null)
                {
                    ProcessEvent(td, events, eventInvocationFuncs, ed, syncEventAttr);
                }
            }
        }

        static void ProcessEvent(TypeDefinition td, List<EventDefinition> events, List<MethodDefinition> eventInvocationFuncs, EventDefinition ed, CustomAttribute syncEventAttr)
        {
            if (ed.EventType.Resolve().HasGenericParameters)
            {
                Weaver.Error($"{ed.Name} must not have generic parameters.  Consider creating a new class that inherits from {ed.EventType} instead", ed);
                return;
            }

            events.Add(ed);
            MethodDefinition eventFunc = ProcessEventInvoke(td, ed);
            if (eventFunc == null)
            {
                return;
            }

            td.Methods.Add(eventFunc);
            eventInvocationFuncs.Add(eventFunc);

            Weaver.DLog(td, "ProcessEvent " + ed);

            MethodDefinition eventCallFunc = ProcessEventCall(td, ed, syncEventAttr);
            td.Methods.Add(eventCallFunc);

            // original weaver compares .Name, not EventDefinition.
            Weaver.WeaveLists.replaceEvents[ed.FullName] = eventCallFunc;

            Weaver.DLog(td, "  Event: " + ed.Name);
        }
    }
}
