namespace Mirror
{
    // snapshot that stores both unscaled and scaled server time.
    // interpolated together in a single pipeline since network
    // conditions are the same for both.
    public struct TimeSnapshot : Snapshot
    {
        public double remoteTime { get; set; }
        public double localTime { get; set; }

        // scaled server time (Time.timeAsDouble), sent in TimeSnapshotMessage.
        // interpolated alongside remoteTime using the same interpolation factor.
        public double remoteScaledTime;

        public TimeSnapshot(double remoteTime, double localTime, double remoteScaledTime = 0)
        {
            this.remoteTime = remoteTime;
            this.localTime = localTime;
            this.remoteScaledTime = remoteScaledTime;
        }
    }
}
