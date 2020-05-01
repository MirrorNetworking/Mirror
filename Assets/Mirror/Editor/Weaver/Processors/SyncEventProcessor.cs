// all the SyncEvent code from NetworkBehaviourProcessor in one place
using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
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

            MethodDefinition cmd = new MethodDefinition("InvokeSyncEvent" + ed.Name, MethodAttributes.Family |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            ILProcessor cmdWorker = cmd.Body.GetILProcessor();
            Instruction label1 = cmdWorker.Create(OpCodes.Nop);
            Instruction label2 = cmdWorker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteClientActiveCheck(cmdWorker, ed.Name, label1, "Event");

            // null event check
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Castclass, td));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldfld, eventField));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Brtrue, label2));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ret));
            cmdWorker.Append(label2);

            // setup reader
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldarg_0));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Castclass, td));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ldfld, eventField));

            // read the event arguments
            MethodReference invoke = Resolvers.ResolveMethod(eventField.FieldType, Weaver.CurrentAssembly, "Invoke");
            if (!NetworkBehaviourProcessor.ProcessNetworkReaderParameters(invoke.Resolve(), cmdWorker, false))
                return null;

            // invoke actual event delegate function
            cmdWorker.Append(cmdWorker.Create(OpCodes.Callvirt, invoke));
            cmdWorker.Append(cmdWorker.Create(OpCodes.Ret));

            NetworkBehaviourProcessor.AddInvokeParameters(cmd.Parameters);

            return cmd;
        }

        public static MethodDefinition ProcessEventCall(TypeDefinition td, EventDefinition ed, CustomAttribute ca)
        {
            MethodReference invoke = Resolvers.ResolveMethod(ed.EventType, Weaver.CurrentAssembly, "Invoke");
            MethodDefinition evt = new MethodDefinition("Call" + ed.Name, MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);
            // add paramters
            foreach (ParameterDefinition pd in invoke.Parameters)
            {
                evt.Parameters.Add(new ParameterDefinition(pd.Name, ParameterAttributes.None, pd.ParameterType));
            }

            ILProcessor evtWorker = evt.Body.GetILProcessor();
            Instruction label = evtWorker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteSetupLocals(evtWorker);

            NetworkBehaviourProcessor.WriteServerActiveCheck(evtWorker, ed.Name, label, "Event");

            NetworkBehaviourProcessor.WriteCreateWriter(evtWorker);

            // write all the arguments that the user passed to the syncevent
            if (!NetworkBehaviourProcessor.WriteArguments(evtWorker, invoke.Resolve(), false))
                return null;

            // invoke interal send and return
            // this
            evtWorker.Append(evtWorker.Create(OpCodes.Ldarg_0));
            evtWorker.Append(evtWorker.Create(OpCodes.Ldtoken, td));
            // invokerClass
            evtWorker.Append(evtWorker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference));
            evtWorker.Append(evtWorker.Create(OpCodes.Ldstr, ed.Name));
            // writer
            evtWorker.Append(evtWorker.Create(OpCodes.Ldloc_0));
            evtWorker.Append(evtWorker.Create(OpCodes.Ldc_I4, ca.GetField("channel", 0)));
            evtWorker.Append(evtWorker.Create(OpCodes.Call, Weaver.sendEventInternal));

            NetworkBehaviourProcessor.WriteRecycleWriter(evtWorker);

            evtWorker.Append(evtWorker.Create(OpCodes.Ret));

            return evt;
        }

        public static void ProcessEvents(TypeDefinition td, List<EventDefinition> events, List<MethodDefinition> eventInvocationFuncs)
        {
            // find events
            foreach (EventDefinition ed in td.Events)
            {
                CustomAttribute ca = ed.GetCustomAttribute(Weaver.SyncEventType.FullName);

                if (ca != null)
                {
                    if (!ed.Name.StartsWith("Event"))
                    {
                        Weaver.Error($"{ed.Name} must start with Event.  Consider renaming it to Event{ed.Name}", ed);
                        return;
                    }

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

                    MethodDefinition eventCallFunc = ProcessEventCall(td, ed, ca);
                    td.Methods.Add(eventCallFunc);

                    // original weaver compares .Name, not EventDefinition.
                    Weaver.WeaveLists.replaceEvents[ed.Name] = eventCallFunc;

                    Weaver.DLog(td, "  Event: " + ed.Name);
                    break;
                }
            }
        }
    }
}
