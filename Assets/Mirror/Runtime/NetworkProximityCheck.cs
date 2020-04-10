

namespace Mirror
{
    // unfortunately the name NetworkProximityChecker is already used by our
    // component that does spherecasts.
    // maybe rename it later.
    //
    // note: we inherit from NetworkBehaviour so we can reuse .netIdentity, etc.
    public abstract class NetworkProximityCheck : NetworkBehaviour
    {
    }
}
