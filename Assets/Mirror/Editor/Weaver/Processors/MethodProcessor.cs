using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public static class MethodProcessor
    {
        const string RpcPrefix = "UserCode_";

        // For a function like
        //   [ClientRpc] void RpcTest(int value),
        // Weaver substitutes the method and moves the code to a new method:
        //   UserCode_RpcTest(int value) <- contains original code
        //   RpcTest(int value) <- serializes parameters, sends the message
        //
        // Note that all the calls to the method remain untouched.
        // FixRemoteCallToBaseMethod replaces them afterwards.
        public static MethodDefinition SubstituteMethod(Logger Log, TypeDefinition td, MethodDefinition md, ref bool WeavingFailed)
        {
            string newName = Weaver.GenerateMethodName(RpcPrefix, md);

            MethodDefinition cmd = new MethodDefinition(newName, md.Attributes, md.ReturnType);

            // force the substitute method to be protected.
            // -> public would show in the Inspector for UnityEvents as
            //    User_CmdUsePotion() etc. but the user shouldn't use those.
            // -> private would not allow inheriting classes to call it, see
            //    OverrideVirtualWithBaseCallsBothVirtualAndBase test.
            // -> IL has no concept of 'protected', it's called IsFamily there.
            cmd.IsPublic = false;
            cmd.IsFamily = true;

            // add parameters
            foreach (ParameterDefinition pd in md.Parameters)
            {
                cmd.Parameters.Add(new ParameterDefinition(pd.Name, ParameterAttributes.None, pd.ParameterType));
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

            td.Methods.Add(cmd);

            FixRemoteCallToBaseMethod(Log, td, cmd, ref WeavingFailed);
            return cmd;
        }

        // For a function like
        //   [ClientRpc] void RpcTest(int value),
        // Weaver substitutes the method and moves the code to a new method:
        //   UserCode_RpcTest(int value) <- contains original code
        //   RpcTest(int value) <- serializes parameters, sends the message
        //
        // FixRemoteCallToBaseMethod replaces all calls to
        //   RpcTest(value)
        // with
        //   UserCode_RpcTest(value)
        public static void FixRemoteCallToBaseMethod(Logger Log, TypeDefinition type, MethodDefinition method, ref bool WeavingFailed)
        {
            string callName = method.Name;

            // Cmd/rpc start with Weaver.RpcPrefix
            // e.g. CallCmdDoSomething
            if (!callName.StartsWith(RpcPrefix))
                return;

            // e.g. CmdDoSomething
            string baseRemoteCallName = method.Name.Substring(RpcPrefix.Length);

            foreach (Instruction instruction in method.Body.Instructions)
            {
                // is this instruction a Call to a method?
                // if yes, output the method so we can check it.
                if (IsCallToMethod(instruction, out MethodDefinition calledMethod))
                {
                    // when considering if 'calledMethod' is a call to 'method',
                    // we originally compared .Name.
                    //
                    // to fix IL2CPP build bugs with overloaded Rpcs, we need to
                    // generated rpc names like
                    //   RpcTest(string value) => RpcTestString(strig value)
                    //   RpcTest(int value)    => RpcTestInt(int value)
                    // to make them unique.
                    //
                    // calledMethod.Name is still "RpcTest", so we need to
                    // convert this to the generated name as well before comparing.
                    string calledMethodName_Generated = Weaver.GenerateMethodName("", calledMethod);
                    if (calledMethodName_Generated == baseRemoteCallName)
                    {
                        TypeDefinition baseType = type.BaseType.Resolve();
                        MethodDefinition baseMethod = baseType.GetMethodInBaseType(callName);

                        if (baseMethod == null)
                        {
                            Log.Error($"Could not find base method for {callName}", method);
                            WeavingFailed = true;
                            return;
                        }

                        if (!baseMethod.IsVirtual)
                        {
                            Log.Error($"Could not find base method that was virtual {callName}", method);
                            WeavingFailed = true;
                            return;
                        }

                        instruction.Operand = baseMethod;
                    }
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
