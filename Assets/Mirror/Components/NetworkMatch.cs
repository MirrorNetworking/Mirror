// simple component that holds match information
using System;

namespace Mirror
{
    public class NetworkMatch : NetworkBehaviour
    {
        ///<summary>Set this to the same value on all networked objects that belong to a given match</summary>
        public Guid matchId;
    }
}
