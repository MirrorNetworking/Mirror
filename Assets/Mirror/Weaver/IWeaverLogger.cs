using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    public interface IWeaverLogger
    {
        void Error(string message);
        void Error(string message, MemberReference mr);
        void Error(string message, MemberReference mr, SequencePoint sequencePoint);
        void Error(string message, MethodDefinition md);
        void Warning(string message);
        void Warning(string message, MemberReference mr);
    }
}