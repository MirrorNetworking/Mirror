To use Performance Benchmark Reporter you must have:
- Unity Performance Testing Extension
- [.NET core SDK](https://dotnet.microsoft.com/download)
- [Performance Benchmark Reporter DLL](https://github.com/Unity-Technologies/PerformanceBenchmarkReporter/releases)

#### Install Unity Performance Testing Extension
- Open  `manifest.json`
- Add 
```json
{
    "dependencies": {
        "com.unity.test-framework": "0.0.4-preview",
        "com.unity.test-framework.performance": "0.1.49-preview",
        /// other dependencies
        },
    "testables": [
        "com.unity.test-framework.performance"
    ]
}
```


# Create a Performance Benchmark Report

0. Set up the `Performance Benchmark Report Builder` window by adding the path to the required files. Window can be found at `Window/Tools/Performance Benchmark Report Builder`

1. Run the performance tests using the test runner to create a `TestResults.xml`
2. Press "move" in the Builder window to copy the result to the path
3. Change branches and run performance tests again
4. Once all results are collected press "Build Report"
5. Open the "UnityPerformanceBenchmark" html file that is created to view the report
    

![image of window](https://user-images.githubusercontent.com/23101891/78310942-52c52100-7547-11ea-969a-662c0d8e8df7.png)


### Performance Benchmark Report Builder Window

The window stores its settings in `Application.persistentDataPath`, Use Save/Load settings buttons at top to manauly save/load. Settings should automatically load/save when window is opened/closed.


# Links

#### Blog post
https://blogs.unity3d.com/2018/09/25/performance-benchmarking-in-unity-how-to-get-started/

#### Unity packages
https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/index.html
https://docs.unity3d.com/Packages/com.unity.test-framework.performance@2.0/manual/index.html



#### Performance Benchmark Reporter

https://github.com/Unity-Technologies/PerformanceBenchmarkReporter/wiki