using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    // Processes [Rpc] methods in NetworkBehaviour
    public static class RpcProcessor
    {
        public static MethodDefinition ProcessRpcInvoke(WeaverTypes weaverTypes, Writers writers, Readers readers, Logger Log, TypeDefinition td, MethodDefinition md, MethodDefinition rpcCallFunc, ref bool WeavingFailed)
        {
            string rpcName = Weaver.GenerateMethodName(Weaver.InvokeRpcPrefix, md);

            MethodDefinition rpc = new MethodDefinition(rpcName, MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                                                        weaverTypes.Import(typeof(void)));

            ILProcessor worker = rpc.Body.GetILProcessor();
            Instruction label = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteClientActiveCheck(worker, weaverTypes, md.Name, label, "RPC");

            // setup for reader
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Castclass, td);

            if (!NetworkBehaviourProcessor.ReadArguments(md, readers, Log, worker, RemoteCallType.ClientRpc, ref WeavingFailed))
                return null;

            // invoke actual command function
            worker.Emit(OpCodes.Callvirt, rpcCallFunc);
            worker.Emit(OpCodes.Ret);

            NetworkBehaviourProcessor.AddInvokeParameters(weaverTypes, rpc.Parameters);
            td.Methods.Add(rpc);
            return rpc;
        }

        /*
         * generates code like:

            public void RpcTest (int param)
            {
                NetworkWriter writer = new NetworkWriter ();
                writer.WritePackedUInt32((uint)param);
                base.SendRPCInternal(typeof(class),"RpcTest", writer, 0);
            }
            public void CallRpcTest (int param)
            {
                // whatever the user did before
            }

            Originally HLAPI put the send message code inside the Call function
            and then proceeded to replace every call to RpcTest with CallRpcTest

            This method moves all the user's code into the "CallRpc" method
            and replaces the body of the original method with the send message code.
            This way we do not need to modify the code anywhere else,  and this works
            correctly in dependent assemblies
        */
        public static MethodDefinition ProcessRpcCall(WeaverTypes weaverTypes, Writers writers, Logger Log, TypeDefinition td, MethodDefinition md, CustomAttribute clientRpcAttr, ref bool WeavingFailed)
        {
            MethodDefinition rpc = MethodProcessor.SubstituteMethod(Log, td, md, ref WeavingFailed);

            ILProcessor worker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(worker, weaverTypes);

            // add a log message if needed for debugging
            //worker.Emit(OpCodes.Ldstr, $"Call ClientRpc function {md.Name}");
            //worker.Emit(OpCodes.Call, WeaverTypes.logErrorReference);

            NetworkBehaviourProcessor.WriteGetWriter(worker, weaverTypes);

            // write all the arguments that the user passed to the Rpc call
            if (!NetworkBehaviourProcessor.WriteArguments(worker, writers, Log, md, RemoteCallType.ClientRpc, ref WeavingFailed))
                return null;

            int channel = clientRpcAttr.GetField("channel", 0);
            bool includeOwner = clientRpcAttr.GetField("includeOwner", true);

            // invoke SendInternal and return
            // this
            worker.Emit(OpCodes.Ldarg_0);
            // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
            worker.Emit(OpCodes.Ldstr, md.FullName);
            // pass the function hash so we don't have to compute it at runtime
            // otherwise each GetStableHash call requires O(N) complexity.
            // noticeable for long function names: 
            // https://github.com/MirrorNetworking/Mirror/issues/3375
            worker.Emit(OpCodes.Ldc_I4, md.FullName.GetStableHashCode());
            // writer
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ldc_I4, channel);
            // includeOwner ? 1 : 0
            worker.Emit(includeOwner ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            worker.Emit(OpCodes.Callvirt, weaverTypes.sendRpcInternal);

            NetworkBehaviourProcessor.WriteReturnWriter(worker, weaverTypes);

            worker.Emit(OpCodes.Ret);

            return rpc;
        }
    }
}
