using Mono.CecilX;

namespace Mirror.Weaver
{
    // not static, because ILPostProcessor is multithreaded
    public interface Logger
    {
        public void Warning(string message);
        public void Warning(string message, MemberReference mr);
        public void Error(string message);
        public void Error(string message, MemberReference mr);
    }
}
