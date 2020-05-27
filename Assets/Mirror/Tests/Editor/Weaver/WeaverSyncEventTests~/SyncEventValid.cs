using Mirror;

namespace WeaverSyncEventTests.SyncEventValid
{
    class SyncEventValid : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate();

        [SyncEvent]
        public event MySyncEventDelegate EventDoCoolThingsWithExcitingPeople;
    }
}
