# Performance Tests

Performance tests require `com.unity.test-framework.performance`


#### Install Unity Performance Testing Extension
When using 2019.1 or earlier you have to manually add these to your `manifest.json` or copy from `Packages/manifest.json` 

- Open  `manifest.json`
- Add 
```json
{
    "dependencies": {
        "com.unity.test-framework.performance": "0.1.50-preview",
        /// other dependencies
        },
    "testables": [
        "com.unity.test-framework.performance"
    ]
}
```


## Links

#### Blog post
<https://blogs.unity3d.com/2018/09/25/performance-benchmarking-in-unity-how-to-get-started/>

#### Unity packages
<https://docs.unity3d.com/Packages/com.unity.test-framework.performance@0.1/manual/index.html>

#### Performance Benchmark Reporter

<https://github.com/Unity-Technologies/PerformanceBenchmarkReporter/wiki>