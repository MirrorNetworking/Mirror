using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    /// <summary>
    /// Processes [Rpc] methods in NetworkBehaviour
    /// </summary>
    public static class RpcProcessor
    {
        public static MethodDefinition ProcessRpcInvoke(TypeDefinition td, MethodDefinition md, MethodDefinition rpcCallFunc)
        {
            MethodDefinition rpc = new MethodDefinition(
                Weaver.InvokeRpcPrefix + md.Name,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                WeaverTypes.Import(typeof(void)));

            ILProcessor worker = rpc.Body.GetILProcessor();
            Instruction label = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteClientActiveCheck(worker, md.Name, label, "RPC");

            // setup for reader
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Castclass, td));

            if (!NetworkBehaviourProcessor.ReadArguments(md, worker, RemoteCallType.ClientRpc))
                return null;

            // invoke actual command function
            worker.Append(worker.Create(OpCodes.Callvirt, rpcCallFunc));
            worker.Append(worker.Create(OpCodes.Ret));

            NetworkBehaviourProcessor.AddInvokeParameters(rpc.Parameters);
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
        public static MethodDefinition ProcessRpcCall(TypeDefinition td, MethodDefinition md, CustomAttribute clientRpcAttr)
        {
            MethodDefinition rpc = MethodProcessor.SubstituteMethod(td, md);

            ILProcessor worker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(worker);

            if (Weaver.GenerateLogErrors)
            {
                worker.Append(worker.Create(OpCodes.Ldstr, "Call ClientRpc function " + md.Name));
                worker.Append(worker.Create(OpCodes.Call, WeaverTypes.logErrorReference));
            }

            NetworkBehaviourProcessor.WriteCreateWriter(worker);

            // write all the arguments that the user passed to the Rpc call
            if (!NetworkBehaviourProcessor.WriteArguments(worker, md, RemoteCallType.ClientRpc))
                return null;

            string rpcName = md.Name;
            int channel = clientRpcAttr.GetField("channel", 0);
            bool excludeOwner = clientRpcAttr.GetField("excludeOwner", false);

            // invoke SendInternal and return
            // this
            worker.Append(worker.Create(OpCodes.Ldarg_0));
            worker.Append(worker.Create(OpCodes.Ldtoken, td));
            // invokerClass
            worker.Append(worker.Create(OpCodes.Call, WeaverTypes.getTypeFromHandleReference));
            worker.Append(worker.Create(OpCodes.Ldstr, rpcName));
            // writer
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldc_I4, channel));
            worker.Append(worker.Create(excludeOwner ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            worker.Append(worker.Create(OpCodes.Callvirt, WeaverTypes.sendRpcInternal));

            NetworkBehaviourProcessor.WriteRecycleWriter(worker);

            worker.Append(worker.Create(OpCodes.Ret));

            return rpc;
        }
    }
}
