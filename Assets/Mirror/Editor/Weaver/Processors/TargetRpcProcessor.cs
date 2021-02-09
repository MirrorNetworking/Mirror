using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    /// <summary>
    /// Processes [TargetRpc] methods in NetworkBehaviour
    /// </summary>
    public static class TargetRpcProcessor
    {
        // helper functions to check if the method has a NetworkConnection parameter
        public static bool HasNetworkConnectionParameter(MethodDefinition md)
        {
            return md.Parameters.Count > 0 &&
                   md.Parameters[0].ParameterType.Is<NetworkConnection>();
        }

        public static MethodDefinition ProcessTargetRpcInvoke(TypeDefinition td, MethodDefinition md, MethodDefinition rpcCallFunc)
        {
            MethodDefinition rpc = new MethodDefinition(Weaver.InvokeRpcPrefix + md.Name, MethodAttributes.Family |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                WeaverTypes.Import(typeof(void)));

            ILProcessor worker = rpc.Body.GetILProcessor();
            Instruction label = worker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteClientActiveCheck(worker, md.Name, label, "TargetRPC");

            // setup for reader
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Castclass, td);

            // NetworkConnection parameter is optional
            if (HasNetworkConnectionParameter(md))
            {
                // if call has NetworkConnection write clients connection as first arg
                //ClientScene.readyconnection
                worker.Emit(OpCodes.Call, WeaverTypes.ReadyConnectionReference);
            }

            // process reader parameters and skip first one if first one is NetworkConnection
            if (!NetworkBehaviourProcessor.ReadArguments(md, worker, RemoteCallType.TargetRpc))
                return null;

            // invoke actual command function
            worker.Emit(OpCodes.Callvirt, rpcCallFunc);
            worker.Emit(OpCodes.Ret);

            NetworkBehaviourProcessor.AddInvokeParameters(rpc.Parameters);
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
        public static MethodDefinition ProcessTargetRpcCall(TypeDefinition td, MethodDefinition md, CustomAttribute targetRpcAttr)
        {
            MethodDefinition rpc = MethodProcessor.SubstituteMethod(td, md);

            ILProcessor worker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(worker);

            NetworkBehaviourProcessor.WriteCreateWriter(worker);

            // write all the arguments that the user passed to the TargetRpc call
            // (skip first one if first one is NetworkConnection)
            if (!NetworkBehaviourProcessor.WriteArguments(worker, md, RemoteCallType.TargetRpc))
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
            worker.Emit(OpCodes.Call, WeaverTypes.getTypeFromHandleReference);
            worker.Emit(OpCodes.Ldstr, rpcName);
            // writer
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ldc_I4, targetRpcAttr.GetField("channel", 0));
            worker.Emit(OpCodes.Callvirt, WeaverTypes.sendTargetRpcInternal);

            NetworkBehaviourProcessor.WriteRecycleWriter(worker);

            worker.Emit(OpCodes.Ret);

            return rpc;
        }
    }
}
