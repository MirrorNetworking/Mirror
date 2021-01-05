using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Mirror.RemoteCalls;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    public abstract class RpcProcessor
    {
        public static string InvokeRpcPrefix => "InvokeUserCode_";

        // helper functions to check if the method has a NetworkConnection parameter
        public bool HasNetworkConnectionParameter(MethodDefinition md)
        {
            return md.Parameters.Count > 0 &&
                   md.Parameters[0].ParameterType.Is<INetworkConnection>();
        }

        public static bool IsNetworkConnection(TypeReference type)
        {
            return type.Resolve().ImplementsInterface<INetworkConnection>();
        }

        public bool WriteArguments(ILProcessor worker, MethodDefinition method, VariableDefinition writer, RemoteCallType callType)
        {
            // write each argument
            // example result
            /*
            writer.WritePackedInt32(someNumber);
            writer.WriteNetworkIdentity(someTarget);
             */

            bool skipFirst = callType == RemoteCallType.ClientRpc
                && HasNetworkConnectionParameter(method);

            // arg of calling  function, arg 0 is "this" so start counting at 1
            int argNum = 1;
            foreach (ParameterDefinition param in method.Parameters)
            {
                // NetworkConnection is not sent via the NetworkWriter so skip it here
                // skip first for NetworkConnection in TargetRpc
                if (argNum == 1 && skipFirst)
                {
                    argNum += 1;
                    continue;
                }
                // skip SenderConnection in ServerRpc
                if (IsNetworkConnection(param.ParameterType))
                {
                    argNum += 1;
                    continue;
                }

                MethodReference writeFunc = method.Module.GetWriteFunc(param.ParameterType);
                if (writeFunc == null)
                {
                    Weaver.Error($"{method.Name} has invalid parameter {param}", method);
                    return false;
                }

                // use built-in writer func on writer object
                // NetworkWriter object
                worker.Append(worker.Create(OpCodes.Ldloc, writer));
                // add argument to call
                worker.Append(worker.Create(OpCodes.Ldarg, argNum));
                // call writer extension method
                worker.Append(worker.Create(OpCodes.Call, writeFunc));
                argNum += 1;
            }
            return true;
        }


        public static bool ReadArguments(MethodDefinition method, ILProcessor worker, bool skipFirst)
        {
            // read each argument
            // example result
            /*
            CallCmdDoSomething(reader.ReadPackedInt32(), reader.ReadNetworkIdentity());
             */

            // arg of calling  function, arg 0 is "this" so start counting at 1
            int argNum = 1;
            foreach (ParameterDefinition param in method.Parameters)
            {
                // NetworkConnection is not sent via the NetworkWriter so skip it here
                // skip first for NetworkConnection in TargetRpc
                if (argNum == 1 && skipFirst)
                {
                    argNum += 1;
                    continue;
                }
                // skip SenderConnection in ServerRpc
                if (IsNetworkConnection(param.ParameterType))
                {
                    argNum += 1;
                    continue;
                }

                MethodReference readFunc = method.Module.GetReadFunc(param.ParameterType);

                if (readFunc == null)
                {
                    Weaver.Error($"{method.Name} has invalid parameter {param}.  Unsupported type {param.ParameterType},  use a supported MirrorNG type instead", method);
                    return false;
                }

                worker.Append(worker.Create(OpCodes.Ldarg_1));
                worker.Append(worker.Create(OpCodes.Call, readFunc));

                // conversion.. is this needed?
                if (param.ParameterType.Is<float>())
                {
                    worker.Append(worker.Create(OpCodes.Conv_R4));
                }
                else if (param.ParameterType.Is<double>())
                {
                    worker.Append(worker.Create(OpCodes.Conv_R8));
                }
            }
            return true;
        }

        // check if a Command/TargetRpc/Rpc function & parameters are valid for weaving
        public static bool ValidateRemoteCallAndParameters(MethodDefinition method, RemoteCallType callType)
        {
            if (method.IsAbstract)
            {
                Weaver.Error("Abstract Rpcs are currently not supported, use virtual method instead", method);
                return false;
            }

            if (method.IsStatic)
            {
                Weaver.Error($"{method.Name} must not be static", method);
                return false;
            }

            if (method.ReturnType.Is<System.Collections.IEnumerator>())
            {
                Weaver.Error($"{method.Name} cannot be a coroutine", method);
                return false;
            }

            if (method.HasGenericParameters)
            {
                Weaver.Error($"{method.Name} cannot have generic parameters", method);
                return false;
            }

            return ValidateParameters(method, callType);
        }

        // check if all Command/TargetRpc/Rpc function's parameters are valid for weaving
        static bool ValidateParameters(MethodReference method, RemoteCallType callType)
        {
            for (int i = 0; i < method.Parameters.Count; ++i)
            {
                ParameterDefinition param = method.Parameters[i];
                if (!ValidateParameter(method, param, callType, i == 0))
                {
                    return false;
                }
            }
            return true;
        }

        // validate parameters for a remote function call like Rpc/Cmd
        static bool ValidateParameter(MethodReference method, ParameterDefinition param, RemoteCallType callType, bool firstParam)
        {
            if (param.IsOut)
            {
                Weaver.Error($"{method.Name} cannot have out parameters", method);
                return false;
            }

            if (param.ParameterType.IsGenericParameter)
            {
                Weaver.Error($"{method.Name} cannot have generic parameters", method);
                return false;
            }

            if (IsNetworkConnection(param.ParameterType))
            {
                if (callType == RemoteCallType.ClientRpc && firstParam)
                {
                    // perfectly fine,  target rpc can receive a network connection as first parameter
                    return true;
                }

                if (callType == RemoteCallType.ServerRpc)
                {
                    return true;
                }

                Weaver.Error($"{method.Name} has invalid parameter {param}, Cannot pass NetworkConnections", method);
                return false;
            }

            if (param.IsOptional)
            {
                Weaver.Error($"{method.Name} cannot have optional parameters", method);
                return false;
            }

            return true;
        }

        public static void CreateRpcDelegate(ILProcessor worker, MethodDefinition func)
        {
            MethodReference CmdDelegateConstructor;


            if (func.ReturnType.Is(typeof(void)))
            {
                ConstructorInfo[] constructors = typeof(CmdDelegate).GetConstructors();
                CmdDelegateConstructor = func.Module.ImportReference(constructors.First());
            }
            else if (func.ReturnType.Is(typeof(UniTask<int>).GetGenericTypeDefinition()))
            {
                var taskReturnType = func.ReturnType as GenericInstanceType;

                TypeReference returnType = taskReturnType.GenericArguments[0];
                TypeReference genericDelegate = func.Module.ImportReference(typeof(RequestDelegate<int>).GetGenericTypeDefinition());

                var delegateInstance = new GenericInstanceType(genericDelegate);
                delegateInstance.GenericArguments.Add(returnType);

                ConstructorInfo constructor = typeof(RequestDelegate<int>).GetConstructors().First();

                MethodReference constructorRef = func.Module.ImportReference(constructor);

                CmdDelegateConstructor = constructorRef.MakeHostInstanceGeneric(delegateInstance);
            }
            else
            {
                Log.Error("Use UniTask<x> to return a value from ServerRpc in" + func);
                return;
            }

            worker.Append(worker.Create(OpCodes.Ldftn, func));
            worker.Append(worker.Create(OpCodes.Newobj, CmdDelegateConstructor));
        }
    }
}