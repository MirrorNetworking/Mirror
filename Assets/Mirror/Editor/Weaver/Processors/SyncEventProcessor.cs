// all the SyncEvent code from NetworkBehaviourProcessor in one place
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
                Weaver.Error("[" + td.Name + "] ERROR: no event field?!");
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
            if (!NetworkBehaviourProcessor.ProcessNetworkReaderParameters(td, invoke.Resolve(), cmdWorker, false))
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
            MethodDefinition evt = new MethodDefinition("Call" +  ed.Name, MethodAttributes.Public |
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
            if (!NetworkBehaviourProcessor.WriteArguments(evtWorker, invoke.Resolve(), "SyncEvent", false))
                return null;

            // invoke interal send and return
            evtWorker.Append(evtWorker.Create(OpCodes.Ldarg_0)); // this
            evtWorker.Append(evtWorker.Create(OpCodes.Ldtoken, td));
            evtWorker.Append(evtWorker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference)); // invokerClass
            evtWorker.Append(evtWorker.Create(OpCodes.Ldstr, ed.Name));
            evtWorker.Append(evtWorker.Create(OpCodes.Ldloc_0)); // writer
            evtWorker.Append(evtWorker.Create(OpCodes.Ldc_I4, NetworkBehaviourProcessor.GetChannelId(ca)));
            evtWorker.Append(evtWorker.Create(OpCodes.Call, Weaver.sendEventInternal));

            evtWorker.Append(evtWorker.Create(OpCodes.Ret));

            return evt;
        }

        public static void ProcessEvents(TypeDefinition td, List<EventDefinition> events, List<MethodDefinition> eventInvocationFuncs)
        {
            // find events
            foreach (EventDefinition ed in td.Events)
            {
                foreach (var ca in ed.CustomAttributes)
                {
                    if (ca.AttributeType.FullName == Weaver.SyncEventType.FullName)
                    {
                        if (ed.Name.Length > 4 && ed.Name.Substring(0, 5) != "Event")
                        {
                            Weaver.Error("Event  [" + td.FullName + ":" + ed.FullName + "] doesnt have 'Event' prefix");
                            return;
                        }

                        if (ed.EventType.Resolve().HasGenericParameters)
                        {
                            Weaver.Error("Event  [" + td.FullName + ":" + ed.FullName + "] cannot have generic parameters");
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

                        Weaver.WeaveLists.replaceEvents[ed.Name] = eventCallFunc; // original weaver compares .Name, not EventDefinition.

                        Weaver.DLog(td, "  Event: " + ed.Name);
                        break;
                    }
                }
            }
        }
    }
}
