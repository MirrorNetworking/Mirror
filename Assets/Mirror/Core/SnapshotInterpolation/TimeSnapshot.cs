namespace Mirror
{
    // empty snapshot that is only used to progress client's local timeline.
    public struct TimeSnapshot : Snapshot
    {
        public double remoteTime { get; set; }
        public double localTime { get; set; }

        public TimeSnapshot(double remoteTime, double localTime)
        {
            this.remoteTime = remoteTime;
            this.localTime = localTime;
        }
    }
}
