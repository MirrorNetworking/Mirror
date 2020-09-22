// Injects server/client active checks for [Server/Client] attributes
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    static class ServerClientAttributeProcessor
    {
        public static bool Process(TypeDefinition td)
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

        static bool ProcessSiteMethod(MethodDefinition md)
        {
            if (md.Name == ".cctor" ||
                md.Name == NetworkBehaviourProcessor.ProcessedFunctionName ||
                md.Name.StartsWith(Weaver.InvokeRpcPrefix))
                return false;

            return ProcessMethodAttributes(md);
        }

        static bool ProcessMethodAttributes(MethodDefinition md)
        {
            bool modified = InjectGuard<ServerAttribute>(md, WeaverTypes.NetworkBehaviourIsServer, "[Server] function '" + md.FullName + "' called on client");

            modified |= InjectGuard<ClientAttribute>(md, WeaverTypes.NetworkBehaviourIsClient, "[Client] function '" + md.FullName + "' called on server");

            modified |= InjectGuard<HasAuthorityAttribute>(md, WeaverTypes.NetworkBehaviourHasAuthority, "[Has Authority] function '" + md.FullName + "' called on player without authority");

            modified |= InjectGuard<LocalPlayerAttribute>(md, WeaverTypes.NetworkBehaviourIsLocalPlayer, "[Local Player] function '" + md.FullName + "' called on nonlocal player");

            return modified;
        }

        static bool InjectGuard<TAttribute>(MethodDefinition md, MethodReference predicate, string message)
        {
            CustomAttribute attribute = md.GetCustomAttribute<TAttribute>();
            if (attribute == null)
                return false;

            if (md.IsAbstract)
            {
                Weaver.Error($" {typeof(TAttribute)} can't be applied to abstract method. Apply to override methods instead.", md);
                return false;
            }

            bool throwError = attribute.GetField("error", true);

            if (!md.DeclaringType.IsDerivedFrom<NetworkBehaviour>())
            {
                Weaver.Error($"{attribute.AttributeType.Name} method {md.Name} must be declared in a NetworkBehaviour", md);
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
                worker.InsertBefore(top, worker.Create(OpCodes.Newobj, WeaverTypes.MethodInvocationExceptionConstructor));
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

        // this is required to early-out from a function with a return value.
        static void InjectGuardReturnValue(MethodDefinition md, ILProcessor worker, Instruction top)
        {
            if (!md.ReturnType.Is(typeof(void)))
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
