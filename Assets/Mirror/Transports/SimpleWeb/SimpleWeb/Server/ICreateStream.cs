using System.Security.Cryptography.X509Certificates;

namespace Mirror.SimpleWeb
{
    public interface ICreateStream
    {
        bool TryCreateStream(IConnection conn);
    }
}
