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
            if (md.Parameters.Count > 0)
            {
                // we need to allow both NetworkConnection, and inheriting types.
                // NetworkBehaviour.SendTargetRpc takes a NetworkConnection parameter.
                // fixes https://github.com/vis2k/Mirror/issues/3290
                TypeReference type = md.Parameters[0].ParameterType;
                return type.Is<NetworkConnection>() ||
                       type.IsDerivedFrom<NetworkConnection>();
            }
            return false;
        }

        public static MethodDefinition ProcessTargetRpcInvoke(WeaverTypes weaverTypes, Readers readers, Logger Log, TypeDefinition td, MethodDefinition md, MethodDefinition rpcCallFunc, ref bool WeavingFailed)
        {
            string trgName = Weaver.GenerateMethodName(RemoteCalls.RemoteProcedureCalls.InvokeRpcPrefix, md);

            MethodDefinition rpc = new MethodDefinition(trgName, MethodAttributes.Family |
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
                // TargetRpcs are sent from server to client.
                // on server, we currently support two types:
                //   TargetRpc(NetworkConnection)
                //   TargetRpc(NetworkConnectionToClient)
                // however, it's always a connection to client.
                // in the future, only NetworkConnectionToClient will be supported.
                // explicit typing helps catch issues at compile time.
                //
                // on client, InvokeTargetRpc calls the original code.
                // we need to fill in the NetworkConnection parameter.
                // NetworkClient.connection is always a connection to server.
                //
                // we used to pass NetworkClient.connection as the TargetRpc parameter.
                // which caused: https://github.com/MirrorNetworking/Mirror/issues/3455
                // when the parameter is defined as a NetworkConnectionToClient.
                //
                // a client's connection never fits into a NetworkConnectionToClient.
                // we need to always pass null here.
                worker.Emit(OpCodes.Ldnull);
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

            NetworkBehaviourProcessor.WriteGetWriter(worker, weaverTypes);

            // write all the arguments that the user passed to the TargetRpc call
            // (skip first one if first one is NetworkConnection)
            if (!NetworkBehaviourProcessor.WriteArguments(worker, writers, Log, md, RemoteCallType.TargetRpc, ref WeavingFailed))
                return null;

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
            // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
            worker.Emit(OpCodes.Ldstr, md.FullName);
            // pass the function hash so we don't have to compute it at runtime
            // otherwise each GetStableHash call requires O(N) complexity.
            // noticeable for long function names:
            // https://github.com/MirrorNetworking/Mirror/issues/3375
            worker.Emit(OpCodes.Ldc_I4, md.FullName.GetStableHashCode());
            // writer
            worker.Emit(OpCodes.Ldloc_0);
            worker.Emit(OpCodes.Ldc_I4, targetRpcAttr.GetField("channel", 0));
            worker.Emit(OpCodes.Callvirt, weaverTypes.sendTargetRpcInternal);

            NetworkBehaviourProcessor.WriteReturnWriter(worker, weaverTypes);

            worker.Emit(OpCodes.Ret);

            return rpc;
        }
    }
}
