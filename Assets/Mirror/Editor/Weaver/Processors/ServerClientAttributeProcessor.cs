// Injects server/client active checks for [Server/Client] attributes
using System;
using System.Linq.Expressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    class ServerClientAttributeProcessor
    {
        private readonly IWeaverLogger logger;

        public ServerClientAttributeProcessor (IWeaverLogger logger)
        {
            this.logger = logger;
        }

        public bool Process(TypeDefinition td)
        {
            bool modified = false;
            foreach (MethodDefinition md in td.Methods)
            {
                modified |= ProcessSiteMethod(md);
            }

            foreach (TypeDefinition nested in td.NestedTypes)
            {
                modified |= Process(nested);
            }
            return modified;
        }

        bool ProcessSiteMethod(MethodDefinition md)
        {
            if (md.Name == ".cctor" ||
                md.Name == NetworkBehaviourProcessor.ProcessedFunctionName ||
                md.Name.StartsWith(RpcProcessor.InvokeRpcPrefix))
                return false;

            return ProcessMethodAttributes(md);
        }

        bool ProcessMethodAttributes(MethodDefinition md)
        {
            bool modified = InjectGuard<ServerAttribute>(md,  nb => nb.IsServer, "[Server] function '" + md.FullName + "' called on client");

            modified |= InjectGuard<ClientAttribute>(md, nb => nb.IsClient, "[Client] function '" + md.FullName + "' called on server");

            modified |= InjectGuard<HasAuthorityAttribute>(md, nb => nb.HasAuthority, "[Has Authority] function '" + md.FullName + "' called on player without authority");

            modified |= InjectGuard<LocalPlayerAttribute>(md, nb => nb.IsLocalPlayer, "[Local Player] function '" + md.FullName + "' called on nonlocal player");

            return modified;
        }

        bool InjectGuard<TAttribute>(MethodDefinition md, Expression<Func<NetworkBehaviour, bool>> predExpression, string message)
        {
            MethodReference predicate = md.Module.ImportReference(predExpression);
            CustomAttribute attribute = md.GetCustomAttribute<TAttribute>();
            if (attribute == null)
                return false;

            if (md.IsAbstract)
            {
                logger.Error($" {typeof(TAttribute)} can't be applied to abstract method. Apply to override methods instead.", md);
                return false;
            }

            bool throwError = attribute.GetField("error", true);

            if (!md.DeclaringType.IsDerivedFrom<NetworkBehaviour>())
            {
                logger.Error($"{attribute.AttributeType.Name} method {md.Name} must be declared in a NetworkBehaviour", md);
                return true;
            }
            ILProcessor worker = md.Body.GetILProcessor();
            Instruction top = md.Body.Instructions[0];

            worker.InsertBefore(top, worker.Create(OpCodes.Ldarg_0));
            worker.InsertBefore(top, worker.Create(OpCodes.Call, predicate));
            worker.InsertBefore(top, worker.Create(OpCodes.Brtrue, top));
            if (throwError)
            {
                worker.InsertBefore(top, worker.Create(OpCodes.Ldstr, message));
                worker.InsertBefore(top, worker.Create(OpCodes.Newobj, () => new MethodInvocationException("")));
                worker.InsertBefore(top, worker.Create(OpCodes.Throw));
            }
            InjectGuardParameters(md, worker, top);
            InjectGuardReturnValue(md, worker, top);
            worker.InsertBefore(top, worker.Create(OpCodes.Ret));
            return true;
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

                    VariableDefinition elementLocal = md.AddLocal(elementType);

                    worker.InsertBefore(top, worker.Create(OpCodes.Ldarg, index + offset));
                    worker.InsertBefore(top, worker.Create(OpCodes.Ldloca_S, elementLocal));
                    worker.InsertBefore(top, worker.Create(OpCodes.Initobj, elementType));
                    worker.InsertBefore(top, worker.Create(OpCodes.Ldloc, elementLocal));
                    worker.InsertBefore(top, worker.Create(OpCodes.Stobj, elementType));
                }
            }
        }

        // this is required to early-out from a function with a return value.
        static void InjectGuardReturnValue(MethodDefinition md, ILProcessor worker, Instruction top)
        {
            if (!md.ReturnType.Is(typeof(void)))
            {
                VariableDefinition returnLocal = md.AddLocal(md.ReturnType);
                worker.InsertBefore(top, worker.Create(OpCodes.Ldloca_S, returnLocal));
                worker.InsertBefore(top, worker.Create(OpCodes.Initobj, md.ReturnType));
                worker.InsertBefore(top, worker.Create(OpCodes.Ldloc, returnLocal));
            }
        }
    }
}
