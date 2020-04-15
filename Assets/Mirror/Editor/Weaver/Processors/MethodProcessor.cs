using Mono.Cecil;
using Mono.Cecil.Cil;

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
            var cmd = new MethodDefinition(newName, md.Attributes, md.ReturnType);

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
            return cmd;
        }
    }
}
