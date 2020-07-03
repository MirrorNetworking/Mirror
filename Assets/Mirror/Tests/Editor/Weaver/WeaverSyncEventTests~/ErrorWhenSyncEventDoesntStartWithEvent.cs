using Mirror;

namespace WeaverSyncEventTests.ErrorWhenSyncEventDoesntStartWithEvent
{
    class ErrorWhenSyncEventDoesntStartWithEvent : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate(int amount, float dir);

        [SyncEvent]
        public event MySyncEventDelegate DoCoolThingsWithExcitingPeople;
    }
}
