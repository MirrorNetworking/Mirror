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
  2024-03-13:      500 FPS Client,   1700 FPS Server
  2024-03-13:      580 FPS Client,   1700 FPS Server // micro optimizations
  2024-03-14:      590 FPS Client,   1700 FPS Server // UpdateGhosting() every 4th frame
  2024-03-14:      615 FPS Client,   1700 FPS Server // predictedRigidbodyTransform.GetPositionAndRotation()
  2024-03-15:      625 FPS Client,   1700 FPS Server // Vector3.MoveTowardsCustom()
  2024-03-18:      628 FPS Client,   1700 FPS Server // removed O(N) insertion from CorrectHistory()
  2024-03-28:      800 FPS Client,   1700 FPS Server // FAST mode prediction
