using System;
using Mono.CecilX;

namespace Mirror.Weaver
{
    // we dont care about serialization for Exceptions
#pragma warning disable CA2229 // Implement serialization constructors

    [Serializable]
    public abstract class WeaverException : Exception
    {
        public MemberReference MemberReference { get; }

        protected WeaverException(string message, MemberReference member) : base(message)
        {
            MemberReference = member;
        }

        protected WeaverException(string message, MemberReference member, Exception innerException) : base(message, innerException)
        {
            MemberReference = member;
        }
    }

    [Serializable]
    public class GenerateWriterException : WeaverException
    {
        public GenerateWriterException(string message, MemberReference member) : base(message, member) { }
    }

    [Serializable]
    public class SyncVarException : WeaverException
    {
        public SyncVarException(MemberReference member, Exception innerException) : base($"Invalid SyncVar: {innerException.Message}", member, innerException) { }
    }

#pragma warning restore CA2229 // Implement serialization constructors
}
