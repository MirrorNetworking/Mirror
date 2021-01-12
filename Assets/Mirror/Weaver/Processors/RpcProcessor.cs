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
        public const string SkeletonPrefix = "Skeleton_";
        public const string UserCodePrefix = "UserCode_";

        protected readonly ModuleDefinition  module;
        protected readonly Readers readers;
        protected readonly Writers writers;
        protected readonly IWeaverLogger logger;

        public static string InvokeRpcPrefix => "InvokeUserCode_";

        protected RpcProcessor(ModuleDefinition module, Readers readers, Writers writers, IWeaverLogger logger)
        {
            this.module = module;
            this.readers = readers;
            this.writers = writers;
            this.logger = logger;
        }

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

                MethodReference writeFunc = writers.GetWriteFunc(param.ParameterType, method.DebugInformation.SequencePoints.FirstOrDefault());
                if (writeFunc == null)
                {
                    logger.Error($"{method.Name} has invalid parameter {param}", method, method.DebugInformation.SequencePoints.FirstOrDefault());
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


        public bool ReadArguments(MethodDefinition method, ILProcessor worker, bool skipFirst)
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

                SequencePoint sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
                MethodReference readFunc = readers.GetReadFunc(param.ParameterType, sequencePoint);

                if (readFunc == null)
                {
                    logger.Error($"{method.Name} has invalid parameter {param}.  Unsupported type {param.ParameterType},  use a supported MirrorNG type instead", method);
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
        public bool ValidateRemoteCallAndParameters(MethodDefinition method, RemoteCallType callType)
        {
            if (method.IsAbstract)
            {
                logger.Error("Abstract Rpcs are currently not supported, use virtual method instead", method);
                return false;
            }

            if (method.IsStatic)
            {
                logger.Error($"{method.Name} must not be static", method);
                return false;
            }

            if (method.ReturnType.Is<System.Collections.IEnumerator>())
            {
                logger.Error($"{method.Name} cannot be a coroutine", method);
                return false;
            }

            if (method.HasGenericParameters)
            {
                logger.Error($"{method.Name} cannot have generic parameters", method);
                return false;
            }

            return ValidateParameters(method, callType);
        }

        // check if all Command/TargetRpc/Rpc function's parameters are valid for weaving
        bool ValidateParameters(MethodReference method, RemoteCallType callType)
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
        bool ValidateParameter(MethodReference method, ParameterDefinition param, RemoteCallType callType, bool firstParam)
        {
            if (param.IsOut)
            {
                logger.Error($"{method.Name} cannot have out parameters", method);
                return false;
            }

            if (param.ParameterType.IsGenericParameter)
            {
                logger.Error($"{method.Name} cannot have generic parameters", method);
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

                logger.Error($"{method.Name} has invalid parameter {param}, Cannot pass NetworkConnections", method);
                return false;
            }

            if (param.IsOptional)
            {
                logger.Error($"{method.Name} cannot have optional parameters", method);
                return false;
            }

            return true;
        }

        public void CreateRpcDelegate(ILProcessor worker, MethodDefinition func)
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
                logger.Error("Use UniTask<x> to return a value from ServerRpc in" + func);
                return;
            }

            worker.Append(worker.Create(OpCodes.Ldftn, func));
            worker.Append(worker.Create(OpCodes.Newobj, CmdDelegateConstructor));
        }

        // creates a method substitute
        // For example, if we have this:
        //  public void CmdThrust(float thrusting, int spin)
        //  {
        //      xxxxx   
        //  }
        //
        //  it will substitute the method and move the code to a new method with a provided name
        //  for example:
        //
        //  public void CmdTrust(float thrusting, int spin)
        //  {
        //  }
        //
        //  public void <newName>(float thrusting, int spin)
        //  {
        //      xxxxx
        //  }
        //
        //  Note that all the calls to the method remain untouched
        //
        //  the original method definition loses all code
        //  this returns the newly created method with all the user provided code
        public MethodDefinition SubstituteMethod(MethodDefinition md)
        {
            string newName = UserCodePrefix + md.Name;
            MethodDefinition cmd = md.DeclaringType.AddMethod(newName, md.Attributes, md.ReturnType);

            // add parameters
            foreach (ParameterDefinition pd in md.Parameters)
            {
                _ = cmd.AddParam(pd.ParameterType, pd.Name);
            }

            // swap bodies
            (cmd.Body, md.Body) = (md.Body, cmd.Body);

            // Move over all the debugging information
            foreach (SequencePoint sequencePoint in md.DebugInformation.SequencePoints)
                cmd.DebugInformation.SequencePoints.Add(sequencePoint);
            md.DebugInformation.SequencePoints.Clear();

            foreach (CustomDebugInformation customInfo in md.CustomDebugInformations)
                cmd.CustomDebugInformations.Add(customInfo);
            md.CustomDebugInformations.Clear();

            (md.DebugInformation.Scope, cmd.DebugInformation.Scope) = (cmd.DebugInformation.Scope, md.DebugInformation.Scope);

            FixRemoteCallToBaseMethod(md.DeclaringType, cmd);
            return cmd;
        }


        /// <summary>
        /// Finds and fixes call to base methods within remote calls
        /// <para>For example, changes `base.CmdDoSomething` to `base.UserCode_CmdDoSomething` within `this.UserCode_CmdDoSomething`</para>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="method"></param>
        public void FixRemoteCallToBaseMethod(TypeDefinition type, MethodDefinition method)
        {
            string callName = method.Name;

            // all ServerRpcs/Rpc start with "UserCode_"
            // eg CallCmdDoSomething
            if (!callName.StartsWith(UserCodePrefix))
                return;

            // eg CmdDoSomething
            string baseRemoteCallName = method.Name.Substring(UserCodePrefix.Length);

            foreach (Instruction instruction in method.Body.Instructions)
            {
                // if call to base.CmdDoSomething within this.CallCmdDoSomething
                if (IsCallToMethod(instruction, out MethodDefinition calledMethod) &&
                    calledMethod.Name == baseRemoteCallName)
                {
                    TypeDefinition baseType = type.BaseType.Resolve();
                    MethodDefinition baseMethod = baseType.GetMethodInBaseType(callName);

                    if (baseMethod == null)
                    {
                        logger.Error($"Could not find base method for {callName}", method);
                        return;
                    }

                    if (!baseMethod.IsVirtual)
                    {
                        logger.Error($"Could not find base method that was virtual {callName}", method);
                        return;
                    }

                    instruction.Operand = method.Module.ImportReference(baseMethod);

                    Weaver.DLog(type, "Replacing call to '{0}' with '{1}' inside '{2}'", calledMethod.FullName, baseMethod.FullName, method.FullName);
                }
            }
        }

        static bool IsCallToMethod(Instruction instruction, out MethodDefinition calledMethod)
        {
            if (instruction.OpCode == OpCodes.Call &&
                instruction.Operand is MethodDefinition method)
            {
                calledMethod = method;
                return true;
            }
            else
            {
                calledMethod = null;
                return false;
            }
        }

    }
}