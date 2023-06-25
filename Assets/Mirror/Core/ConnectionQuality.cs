// standalone, Unity-independent connection-quality algorithm & enum.
// don't need to use this directly, it's built into Mirror's NetworkClient.
namespace Mirror
{
    public enum ConnectionQuality : byte
    {
        EXCELLENT,
        GOOD,
        FAIR,
        POOR,
        ESTIMATING,
    }

    // provide different heuristics for users to choose from.
    // simple heuristics to get started.
    // this will be iterated on over time based on user feedback.
    public static class ConnectionQualityHeuristics
    {
        // straight forward estimation
        //   rtt: average round trip time in seconds.
        //   jitter: average latency variance.
        public static ConnectionQuality Simple(double rtt, double jitter)
        {
            // 50 ms ping = 100 ms rtt, and 10ms jitter
            if (rtt <= 0.100 && jitter <= 0.10)
                return ConnectionQuality.EXCELLENT;

            // 100 ms ping = 200 ms rtt, and 20ms jitter
            if (rtt <= 0.200 && jitter <= 0.20)
                return ConnectionQuality.GOOD;

            // 200 ms ping = 400 ms rtt, and 50ms jitter
            if (rtt <= 0.400 && jitter <= 0.50)
                return ConnectionQuality.FAIR;

            // everything else is poor
            return ConnectionQuality.POOR;
        }

        // snapshot interpolation based estimation.
        // snap. interp. adjusts buffer time based on connection quality.
        // based on this, we can measure how far away we are from the ideal.
        // the returned quality will always directly correlate with gameplay.
        // => requires SnapshotInterpolation dynamicAdjustment to be enabled!
        public static ConnectionQuality Pragmatic(double targetBufferTime, double currentBufferTime)
        {
            // buffer time is set by the game developer.
            // estimating in multiples is a great way to be game independent.
            // for example, a fast paced shooter and a slow paced RTS will both
            // have poor connection if the multiplier is >10.
            double multiplier = currentBufferTime / targetBufferTime;

            // dynamic adjustment may even reduce multiplier on great connections
            if (multiplier <= 1.0) return ConnectionQuality.EXCELLENT;

            // 150% multiplier: 50ms => 75ms is still good
            if (multiplier <= 1.5) return ConnectionQuality.GOOD;

            // 200% multiplier: 50ms => 100ms is still fair
            if (multiplier <= 2.0) return ConnectionQuality.FAIR;

            // anything else is poor
            return ConnectionQuality.POOR;
        }
    }
}
