namespace Telepathy
{
    public class MagnificentReceivePipe : MagnificentPipe
    {
        // connect/disconnect messages can be simple flags
        // no need have a Message<EventType, data> queue if connect & disconnect
        // only happen once.
        bool connectedFlag;
        bool disconnectedFlag;

        // constructor
        public MagnificentReceivePipe(int MaxMessageSize) : base(MaxMessageSize) {}

        // set connected/disconnected flags
        public void SetConnected() { lock (this) { connectedFlag = true; } }
        public void SetDisconnected() { lock (this) { disconnectedFlag = true; } }

        // check & reset connected/disconnected flags
        // => immediately resets them so Tick() doesn't process (dis)connected
        //    multiple times!
        public bool CheckConnected()
        {
            lock (this)
            {
                bool result = connectedFlag;
                connectedFlag = false;
                return result;
            }
        }

        public bool CheckDisconnected()
        {
            lock (this)
            {
                bool result = disconnectedFlag;
                disconnectedFlag = false;
                return result;
            }
        }

        // overwrite clear to also clear the flags
        public override void Clear()
        {
            // pool & queue usage always needs to be locked
            lock (this)
            {
                // base clears queue & pool
                base.Clear();

                // clear flags
                connectedFlag = false;
                disconnectedFlag = false;
            }
        }
    }
}