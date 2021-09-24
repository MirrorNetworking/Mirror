using Mirror;

namespace WeaverSyncObjectsTest.SyncObjectsMoreThanMax
{
    public class RecommendsReadonly : NetworkBehaviour
    {
        // NOT readonly. should show weaver recommendation.
        public SyncList<int> list = new SyncList<int>();
    }
}
