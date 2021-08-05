// Snapshot interface so we can reuse it for all kinds of systems.
// for example, NetworkTransform, NetworkRigidbody, CharacterController etc.
// NOTE: we use '<T>' and 'where T : Snapshot' to avoid boxing.
//       List<Snapshot> would cause allocations through boxing.
namespace Mirror
{
    public interface Snapshot
    {
        // snapshots have two timestamps:
        // -> the remote timestamp (when it was sent by the remote)
        //    used to interpolate.
        // -> the local timestamp (when we received it)
        //    used to know if the first two snapshots are old enough to start.
        //
        // IMPORTANT: the timestamp does _NOT_ need to be sent over the
        //            network. simply get it from batching.
        double remoteTimestamp { get; set; }
        double localTimestamp { get; set; }
    }
}
