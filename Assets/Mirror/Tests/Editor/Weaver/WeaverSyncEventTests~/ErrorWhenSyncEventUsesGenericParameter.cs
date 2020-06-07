using Mirror;

namespace WeaverSyncEventTests.ErrorWhenSyncEventUsesGenericParameter
{
    class ErrorWhenSyncEventUsesGenericParameter : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate<T>(T amount, float dir);

        [SyncEvent]
        public event MySyncEventDelegate<int> EventDoCoolThingsWithExcitingPeople;
    }
}
