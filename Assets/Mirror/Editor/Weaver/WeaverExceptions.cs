using System;
using Mono.CecilX;

namespace Mirror.Weaver
{
    [Serializable]
    public abstract class WeaverException : Exception
    {
        public MemberReference MemberReference { get; }

        protected WeaverException(string message, MemberReference member) : base(message)
        {
            MemberReference = member;
        }

        protected WeaverException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext) : base(serializationInfo, streamingContext) {}
    }

    [Serializable]
    public class GenerateWriterException : WeaverException
    {
        public GenerateWriterException(string message, MemberReference member) : base(message, member) {}
        protected GenerateWriterException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext) : base(serializationInfo, streamingContext) {}
    }
}
