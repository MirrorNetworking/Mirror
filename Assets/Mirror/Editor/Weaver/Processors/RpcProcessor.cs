// all the [Rpc] code from NetworkBehaviourProcessor in one place
using Mono.Cecil;
using Mono.Cecil.Cil;
namespace Mirror.Weaver
{
    public static class RpcProcessor
    {
        public const string k_RpcPrefix = "InvokeRpc";

        public static MethodDefinition ProcessRpcInvoke(TypeDefinition td, MethodDefinition md)
        {
            MethodDefinition rpc = new MethodDefinition(k_RpcPrefix + md.Name, MethodAttributes.Family |
                                                                               MethodAttributes.Static |
                                                                               MethodAttributes.HideBySig,
                Weaver.voidType);

            ILProcessor rpcWorker = rpc.Body.GetILProcessor();
            Instruction label = rpcWorker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteClientActiveCheck(rpcWorker, md.Name, label, "RPC");

            // setup for reader
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldarg_0));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Castclass, td));

            if (!NetworkBehaviourProcessor.ProcessNetworkReaderParameters(td, md, rpcWorker, false))
                return null;

            // invoke actual command function
            rpcWorker.Append(rpcWorker.Create(OpCodes.Callvirt, md));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ret));

            NetworkBehaviourProcessor.AddInvokeParameters(rpc.Parameters);

            return rpc;
        }

        /* generates code like:
        public void CallRpcTest (int param)
        {
            NetworkWriter writer = new NetworkWriter ();
            writer.WritePackedUInt32((uint)param);
            base.SendRPCInternal(typeof(class),"RpcTest", writer, 0);
        }
        */
        public static MethodDefinition ProcessRpcCall(TypeDefinition td, MethodDefinition md, CustomAttribute ca)
        {
            MethodDefinition rpc = new MethodDefinition("Call" +  md.Name, MethodAttributes.Public |
                    MethodAttributes.HideBySig,
                    Weaver.voidType);

            // add paramters
            foreach (ParameterDefinition pd in md.Parameters)
            {
                rpc.Parameters.Add(new ParameterDefinition(pd.Name, ParameterAttributes.None, pd.ParameterType));
            }

            ILProcessor rpcWorker = rpc.Body.GetILProcessor();
            Instruction label = rpcWorker.Create(OpCodes.Nop);

            NetworkBehaviourProcessor.WriteSetupLocals(rpcWorker);

            NetworkBehaviourProcessor.WriteCreateWriter(rpcWorker);

            // write all the arguments that the user passed to the Rpc call
            if (!NetworkBehaviourProcessor.WriteArguments(rpcWorker, md, "RPC", false))
                return null;

            var rpcName = md.Name;
            int index = rpcName.IndexOf(k_RpcPrefix);
            if (index > -1)
            {
                rpcName = rpcName.Substring(k_RpcPrefix.Length);
            }

            // invoke SendInternal and return
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldarg_0)); // this
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldtoken, td));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Call, Weaver.getTypeFromHandleReference)); // invokerClass
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldstr, rpcName));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldloc_0)); // writer
            rpcWorker.Append(rpcWorker.Create(OpCodes.Ldc_I4, NetworkBehaviourProcessor.GetChannelId(ca)));
            rpcWorker.Append(rpcWorker.Create(OpCodes.Callvirt, Weaver.sendRpcInternal));

            rpcWorker.Append(rpcWorker.Create(OpCodes.Ret));

            return rpc;
        }

        public static bool ProcessMethodsValidateRpc(TypeDefinition td, MethodDefinition md, CustomAttribute ca)
        {
            if (md.Name.Length > 2 && md.Name.Substring(0, 3) != "Rpc")
            {
                Weaver.Error("Rpc function [" + td.FullName + ":" + md.Name + "] doesnt have 'Rpc' prefix");
                return false;
            }

            if (md.IsStatic)
            {
                Weaver.Error("ClientRpc function [" + td.FullName + ":" + md.Name + "] cant be a static method");
                return false;
            }

            // validate
            return NetworkBehaviourProcessor.ProcessMethodsValidateFunction(td, md, "Rpc") &&
                   NetworkBehaviourProcessor.ProcessMethodsValidateParameters(td, md, ca, "Rpc");
        }
    }
}
