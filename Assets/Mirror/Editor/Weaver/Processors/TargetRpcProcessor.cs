using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    // Processes [TargetRpc] methods in NetworkBehaviour
    public static class TargetRpcProcessor
    {
        // helper functions to check if the method has a NetworkConnection parameter
        public static bool HasNetworkConnectionParameter(MethodDefinition md)
        {
            return md.Parameters.Count > 0 &&
                   md.Parameters[0].ParameterType.Is<NetworkConnection>();
        }

        public static MethodDefinition ProcessTargetRpcInvoke(WeaverTypes weaverTypes, Readers readers, Logger Log, TypeDefinition td, MethodDefinition md, MethodDefinition rpcCallFunc, ref bool WeavingFailed)
        {
            MethodDefinition rpc = new MethodDefinition(Weaver.InvokeRpcPrefix + md.Name, MethodAttributes.Family |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                weaverTypes.Import(typeof(void)));

            ILProcessor worker = rpc.Body.GetILProcessor();
            Instruction label = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteClientActiveCheck(worker, weaverTypes, md.Name, label, "TargetRPC");

            // setup for reader
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Castclass, td);

            // NetworkConnection parameter is optional
            if (HasNetworkConnectionParameter(md))
            {
                // on server, the NetworkConnection parameter is a connection to client.
                // when the rpc is invoked on the client, it still has the same
                // function signature. we pass in the connection to server,
                // which is cleaner than just passing null)
                //NetworkClient.readyconnection
                //
                // TODO
                // a) .connectionToServer = best solution. no doubt.
                // b) NetworkClient.connection for now. add TODO to not use static later.
                worker.Emit(OpCodes.Call, weaverTypes.ReadyConnectionReference);
            }

            // process reader parameters and skip first one if first one is NetworkConnection
            if (!NetworkBehaviourProcessor.ReadArguments(md, readers, Log, worker, RemoteCallType.TargetRpc, ref WeavingFailed))
                return null;

            // invoke actual command function
            worker.Emit(OpCodes.Callvirt, rpcCallFunc);
            worker.Emit(OpCodes.Ret);

            NetworkBehaviourProcessor.AddInvokeParameters(weaverTypes, rpc.Parameters);
            td.Methods.Add(rpc);
            return rpc;
        }

        /* generates code like:
            public void TargetTest (NetworkConnection conn, int param)
            {
                NetworkWriter writer = new NetworkWriter ();
                writer.WritePackedUInt32 ((uint)param);
                base.SendTargetRPCInternal (conn, typeof(class), "TargetTest", val);
            }
            public void CallTargetTest (NetworkConnection conn, int param)
            {
                // whatever the user did before
            }

            or if optional:
            public void TargetTest (int param)
            {
                NetworkWriter writer = new NetworkWriter ();
                writer.WritePackedUInt32 ((uint)param);
                base.SendTargetRPCInternal (null, typeof(class), "TargetTest", val);
            }
            public void CallTargetTest (int param)
            {
                // whatever the user did before
            }

            Originally HLAPI put the send message code inside the Call function
            and then proceeded to replace every call to TargetTest with CallTargetTest

            This method moves all the user's code into the "CallTargetRpc" method
            and replaces the body of the original method with the send message code.
            This way we do not need to modify the code anywhere else,  and this works
            correctly in dependent assemblies

        */
        public static MethodDefinition ProcessTargetRpcCall(WeaverTypes weaverTypes, Writers writers, Logger Log, TypeDefinition td, MethodDefinition md, CustomAttribute targetRpcAttr, ref bool WeavingFailed)
        {
            MethodDefinition rpc = MethodProcessor.SubstituteMethod(Log, td, md, ref WeavingFailed);

            ILProcessor worker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(worker, weaverTypes);

            NetworkBehaviourProcessor.WriteCreateWriter(worker, weaverTypes);

            // write all the arguments that the user passed to the TargetRpc call
            // (skip first one if first one is NetworkConnection)
            if (!NetworkBehaviourProcessor.WriteArguments(worker, writers, Log, md, RemoteCallType.TargetRpc, ref WeavingFailed))
                return null;

            string rpcName = md.Name;

            // invoke SendInternal and return
            // this
            worker.Emit(OpCodes.Ldarg_0);
            if (HasNetworkConnectionParameter(md))
            {
                // connection
                worker.Emit(OpCodes.Ldarg_1);
            }
            else
            {
                // null
                worker.Emit(OpCodes.Ldnull);
            }
            worker.Emit(OpCodes.Ldtoken, td);
            // invokerClass
            worker.Emit(OpCodes.Call, weaverTypes.getTypeFromHandleReference);
            worker.Emit(OpCodes.Ldstr, rpcName);
            // writer
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ldc_I4, targetRpcAttr.GetField("channel", 0));
            worker.Emit(OpCodes.Callvirt, weaverTypes.sendTargetRpcInternal);

            NetworkBehaviourProcessor.WriteRecycleWriter(worker, weaverTypes);

            worker.Emit(OpCodes.Ret);

            return rpc;
        }
    }
}
