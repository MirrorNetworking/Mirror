using System.Collections.Generic;
using System.Net;

namespace Mirror.KCP
{

    public class IPEndpointComparer : IEqualityComparer<IPEndPoint>
    {
        public bool Equals(IPEndPoint x, IPEndPoint y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(IPEndPoint obj)
        {
            // ideally we would use the ip address as well
            // but in the profiler it shows as extremely expensive
            // so just hash by the port
            return obj.Port;
        }
    }
}
