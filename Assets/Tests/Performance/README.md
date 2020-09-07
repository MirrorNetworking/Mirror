# Performance Tests

Performance tests require `com.unity.test-framework.performance`

#### Install Unity Performance Testing Extension
When using 2019.1 or earlier you have to manually add these to your `manifest.json` or copy from Mirror's `Packages/manifest.json` 

- Open  `manifest.json`
- Add 
```json
{
    "dependencies": {
        "com.unity.test-framework.performance": "0.1.50-preview",
        "other dependencies here"
        },
    "testables": [
        "com.unity.test-framework.performance"
    ]
}
```

## Run Tests from CLI 

[Unity CLI Documentation](https://docs.unity3d.com/2018.4/Documentation/Manual/CommandLineArguments.html)

Options:
- `-testResults` Where results are saved
- `-testPlatform` Use `editmode` or `playmode` to pick which tests to run
- `-testCategory` Comma separated list of test categories
- `-testsFilter` Comma separated list of test names
- `-logFile` change log path, Default path `%LOCALAPPDATA%\Unity\Editor\Editor.log`

Example of running Benchmark tests from cli
```
Unity.exe -testResults /path/to/send/results.xml -runTests -testPlatform playmode -projectPath G:\UnityProjects\Mirror -batchmode -testCategory Benchmark
```

## Create a Performance Benchmark Report

To use Performance Benchmark Reporter you must have:
  - [.NET core SDK](https://dotnet.microsoft.com/download)
  - [Performance Benchmark Reporter DLL](https://github.com/Unity-Technologies/PerformanceBenchmarkReporter/releases)
  - `test-framework.performance` Package at version `0.1.50` or earlier (available Unity versions 2018.3 or 2018.4) 
  
The Performance Benchmark Reporter does not work with newer versions of the test-framework because unity has modified the results XML. The Reporter is open source so it is possible to modify it to work with later versions at some point in the future.


### Run the Performance Benchmark Reporter

1. Run the performance tests to create a `TestResults.xml`
2. If running from editor, move the generated `TestResults.xml` file
3. Change branches and run performance tests again
4. Once all results are collected run the Performance Benchmark Reporter DLL
5. Open the "UnityPerformanceBenchmark" html file that is created to view the report

```
dotnet UnityPerformanceBenchmarkReporter.dll --baseline=D:\UnityPerf\baseline.xml --results=D:\UnityPerf\results --reportdirpath=d:\UnityPerf
```


## Links

#### Blog post
<https://blogs.unity3d.com/2018/09/25/performance-benchmarking-in-unity-how-to-get-started/>

#### Unity packages
<https://docs.unity3d.com/Packages/com.unity.test-framework.performance@0.1/manual/index.html>

#### Performance Benchmark Reporter

<https://github.com/Unity-Technologies/PerformanceBenchmarkReporter/wiki>