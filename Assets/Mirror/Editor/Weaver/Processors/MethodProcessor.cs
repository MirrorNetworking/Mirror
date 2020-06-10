using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public static class MethodProcessor
    {

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
        public static MethodDefinition SubstituteMethod(TypeDefinition td, MethodDefinition md, string newName)
        {
            MethodDefinition cmd = new MethodDefinition(newName, md.Attributes, md.ReturnType);

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

            FixRemoteCallToBaseMethod(td, cmd);
            return cmd;
        }

        /// <summary>
        /// Finds and fixes call to base methods within remote calls
        /// <para>For example, changes `base.CmdDoSomething` to `base.CallCmdDoSomething` within `this.CallCmdDoSomething`</para>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="method"></param>
        public static void FixRemoteCallToBaseMethod(TypeDefinition type, MethodDefinition method)
        {
            string callName = method.Name;

            // all Commands/Rpc start with "Call"
            // eg CallCmdDoSomething
            if (!callName.StartsWith("Call"))
                return;

            // eg CmdDoSomething
            string baseRemoteCallName = method.Name.Substring(4);

            foreach (Instruction instruction in method.Body.Instructions)
            {
                // if call to base.CmdDoSomething within this.CallCmdDoSomething
                if (IsCallToMethod(instruction, out MethodDefinition calledMethod) &&
                    calledMethod.Name == baseRemoteCallName)
                {
                    TypeDefinition baseType = type.BaseType.Resolve();
                    MethodDefinition baseMethod = baseType.GetMethod(callName);

                    instruction.Operand = baseMethod;

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
