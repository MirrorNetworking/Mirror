// this class processes SyncVars, Cmds, Rpcs, etc. of NetworkBehaviours

using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    static class ServerClientAttributeProcessor
    {
        public static void ProcessMethodAttributes(TypeDefinition td, MethodDefinition md)
        {
            foreach (CustomAttribute attr in md.CustomAttributes)
            {
                switch (attr.Constructor.DeclaringType.ToString())
                {
                    case "Mirror.ServerAttribute":
                        InjectServerGuard(td, md, true);
                        break;
                    case "Mirror.ServerCallbackAttribute":
                        InjectServerGuard(td, md, false);
                        break;
                    case "Mirror.ClientAttribute":
                        InjectClientGuard(td, md, true);
                        break;
                    case "Mirror.ClientCallbackAttribute":
                        InjectClientGuard(td, md, false);
                        break;
                    default:
                        break;
                }
            }
        }

        static void InjectServerGuard(TypeDefinition td, MethodDefinition md, bool logWarning)
        {
            if (!Weaver.IsNetworkBehaviour(td))
            {
                Weaver.Error($"Server method {md.Name} must be declared in a NetworkBehaviour", md);
                return;
            }
            ILProcessor worker = md.Body.GetILProcessor();
            Instruction top = md.Body.Instructions[0];

            worker.InsertBefore(top, worker.Create(OpCodes.Call, Weaver.NetworkServerGetActive));
            worker.InsertBefore(top, worker.Create(OpCodes.Brtrue, top));
            if (logWarning)
            {
                worker.InsertBefore(top, worker.Create(OpCodes.Ldstr, "[Server] function '" + md.FullName + "' called on client"));
                worker.InsertBefore(top, worker.Create(OpCodes.Call, Weaver.logWarningReference));
            }
            InjectGuardParameters(md, worker, top);
            InjectGuardReturnValue(md, worker, top);
            worker.InsertBefore(top, worker.Create(OpCodes.Ret));
        }

        static void InjectClientGuard(TypeDefinition td, MethodDefinition md, bool logWarning)
        {
            if (!Weaver.IsNetworkBehaviour(td))
            {
                Weaver.Error($"Client method {md.Name} must be declared in a NetworkBehaviour", md);
                return;
            }
            ILProcessor worker = md.Body.GetILProcessor();
            Instruction top = md.Body.Instructions[0];

            worker.InsertBefore(top, worker.Create(OpCodes.Call, Weaver.NetworkClientGetActive));
            worker.InsertBefore(top, worker.Create(OpCodes.Brtrue, top));
            if (logWarning)
            {
                worker.InsertBefore(top, worker.Create(OpCodes.Ldstr, "[Client] function '" + md.FullName + "' called on server"));
                worker.InsertBefore(top, worker.Create(OpCodes.Call, Weaver.logWarningReference));
            }

            InjectGuardParameters(md, worker, top);
            InjectGuardReturnValue(md, worker, top);
            worker.InsertBefore(top, worker.Create(OpCodes.Ret));
        }

        // this is required to early-out from a function with "ref" or "out" parameters
        static void InjectGuardParameters(MethodDefinition md, ILProcessor worker, Instruction top)
        {
            int offset = md.Resolve().IsStatic ? 0 : 1;
            for (int index = 0; index < md.Parameters.Count; index++)
            {
                ParameterDefinition param = md.Parameters[index];
                if (param.IsOut)
                {
                    TypeReference elementType = param.ParameterType.GetElementType();
                    if (elementType.IsPrimitive)
                    {
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldarg, index + offset));
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldc_I4_0));
                        worker.InsertBefore(top, worker.Create(OpCodes.Stind_I4));
                    }
                    else
                    {
                        md.Body.Variables.Add(new VariableDefinition(elementType));
                        md.Body.InitLocals = true;

                        worker.InsertBefore(top, worker.Create(OpCodes.Ldarg, index + offset));
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldloca_S, (byte)(md.Body.Variables.Count - 1)));
                        worker.InsertBefore(top, worker.Create(OpCodes.Initobj, elementType));
                        worker.InsertBefore(top, worker.Create(OpCodes.Ldloc, md.Body.Variables.Count - 1));
                        worker.InsertBefore(top, worker.Create(OpCodes.Stobj, elementType));
                    }
                }
            }
        }

        // this is required to early-out from a function with a return value.
        static void InjectGuardReturnValue(MethodDefinition md, ILProcessor worker, Instruction top)
        {
            if (md.ReturnType.FullName != Weaver.voidType.FullName)
            {
                if (md.ReturnType.IsPrimitive)
                {
                    worker.InsertBefore(top, worker.Create(OpCodes.Ldc_I4_0));
                }
                else
                {
                    md.Body.Variables.Add(new VariableDefinition(md.ReturnType));
                    md.Body.InitLocals = true;

                    worker.InsertBefore(top, worker.Create(OpCodes.Ldloca_S, (byte)(md.Body.Variables.Count - 1)));
                    worker.InsertBefore(top, worker.Create(OpCodes.Initobj, md.ReturnType));
                    worker.InsertBefore(top, worker.Create(OpCodes.Ldloc, md.Body.Variables.Count - 1));
                }
            }
        }
    }
}
