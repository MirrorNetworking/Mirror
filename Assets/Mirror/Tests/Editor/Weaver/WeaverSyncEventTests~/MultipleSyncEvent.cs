using Mirror;

namespace WeaverSyncEventTests.MultipleSyncEvent
{
    class MultipleSyncEvent : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate();
        public delegate void MySyncEventDelegate2(int someNumber);

        [SyncEvent]
        public event MySyncEventDelegate EventDoCoolThingsWithExcitingPeople;

        [SyncEvent]
        public event MySyncEventDelegate2 EventDoMoreCoolThingsWithExcitingPeople;
    }
}
