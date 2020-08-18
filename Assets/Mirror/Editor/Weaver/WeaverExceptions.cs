using System;
using Mono.CecilX;

namespace Mirror.Weaver
{
    [Serializable]
    public abstract class WeaverException : Exception
    {
        public MemberReference MemberReference { get; }

        public WeaverException(string message, MemberReference member) : base(message)
        {
            MemberReference = member;
        }

        public WeaverException(string message, Exception innerException) : base(message, innerException) { }

        protected WeaverException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }

    [Serializable]
    public class GenerateWriterException : WeaverException
    {
        public GenerateWriterException(string message, MemberReference member) : base(message, member) { }
        public GenerateWriterException(string message, Exception innerException) : base(message, innerException) { }
        protected GenerateWriterException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }

    [Serializable]
    public class SyncObjectException : WeaverException
    {
        public SyncObjectException(string message, MemberReference member) : base(message, member) { }
        public SyncObjectException(string message, Exception innerException) : base(message, innerException) { }
        protected SyncObjectException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}
