Mirror's PredictedRigidbody is optimized for low end devices / VR.
While not interacting with the object, there's zero overhead!
While interacting, overhead comes from sync & corrections.

This benchmark has predicted objects which are constantly synced & corrected.
=> This is not a real world scenario, it's worst case that we can use for profiling!
=> As a Mirror user you don't need to worry about this demo.

# Benchmark Setup
- Unity 2021.3 LTS
- IL2CPP Builds
- M1 Macbook Pro
- vsync disabled in NetworkManagerPredictionBenchmark.cs

# Benchmark Results History for 1000 objects without ghosts:
Not Predicted:    1000 FPS Client,   2500 FPS Server
Predicted:         
2024-03-13:       500 FPS Client,   1700 FPS Server
2024-03-13:       580 FPS Client,   1700 FPS Server // don't log hard correcting msgs
