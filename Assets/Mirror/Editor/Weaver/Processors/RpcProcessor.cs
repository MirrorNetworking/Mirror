// all the [Rpc] code from NetworkBehaviourProcessor in one place
using Mono.CecilX;
using Mono.CecilX.Cil;
namespace Mirror.Weaver
{
    public static class RpcProcessor
    {
        public const string RpcPrefix = "InvokeRpc";

        public static MethodDefinition ProcessRpcInvoke(TypeDefinition td, MethodDefinition md, MethodDefinition rpcCallFunc)
        {
            MethodDefinition rpc = new MethodDefinition(
                RpcPrefix + md.Name,
                MethodAttributes.Family | MethodAttributes.Static | MethodAttributes.HideBySig,
                Weaver.voidType);

            ILProcessor rpcWorker = rpc.Body.GetILProcessor();
            Instruction label = rpcWorker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteClientActiveCheck(rpcWorker, md.Name, label, "RPC");

            // setup for reader
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldarg_0));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Castclass, td));

            if (!NetworkBehaviourProcessor.ProcessNetworkReaderParameters(md, rpcWorker, false))
                return null;

            // invoke actual command function
            rpcWorker.Append(rpcWorker.Create(OpCodes.Callvirt, rpcCallFunc));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ret));

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

            This method moves all the user's code into the "Call" method
            and replaces the body of the original method with the send message code.
            This way we do not need to modify the code anywhere else,  and this works
            correctly in dependent assemblies
        */
        public static MethodDefinition ProcessRpcCall(TypeDefinition td, MethodDefinition md, CustomAttribute ca)
        {
            MethodDefinition rpc = MethodProcessor.SubstituteMethod(td, md, "Call" + md.Name);

            ILProcessor rpcWorker = md.Body.GetILProcessor();

            NetworkBehaviourProcessor.WriteSetupLocals(rpcWorker);

            NetworkBehaviourProcessor.WriteCreateWriter(rpcWorker);

            // write all the arguments that the user passed to the Rpc call
            if (!NetworkBehaviourProcessor.WriteArguments(rpcWorker, md, false))
                return null;

            string rpcName = md.Name;
            int index = rpcName.IndexOf(RpcPrefix);
            if (index > -1)
            {
                rpcName = rpcName.Substring(RpcPrefix.Length);
            }

            // invoke SendInternal and return
            // this
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldarg_0));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldtoken, td));
            // invokerClass
            rpcWorker.Append(rpcWorker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldstr, rpcName));
            // writer
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldloc_0));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldc_I4, ca.GetField("channel", 0)));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Callvirt, Weaver.sendRpcInternal));

            NetworkBehaviourProcessor.WriteRecycleWriter(rpcWorker);

            rpcWorker.Append(rpcWorker.Create(OpCodes.Ret));

            return rpc;
        }

        public static bool ProcessMethodsValidateRpc(MethodDefinition md, CustomAttribute ca)
        {
            if (!md.Name.StartsWith("Rpc"))
            {
                Weaver.Error($"{md.Name} must start with Rpc.  Consider renaming it to Rpc{md.Name}", md);
                return false;
            }

            if (md.IsStatic)
            {
                Weaver.Error($"{md.Name} must not be static", md);
                return false;
            }

            // validate
            return NetworkBehaviourProcessor.ProcessMethodsValidateFunction(md) &&
                   NetworkBehaviourProcessor.ProcessMethodsValidateParameters(md, ca);
        }
    }
}
