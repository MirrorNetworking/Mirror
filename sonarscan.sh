#!/bin/bash -e

#Before running this script

# 1) dotnet tool install --global dotnet-sonarscanner --version 4.8.0
# 2) add ~/.dotnet/tools to the path
# 3) edit ~/.dotnet/tools/.store/dotnet-sonarscanner/4.8.0/dotnet-sonarscanner/4.8.0/tools/netcoreapp3.0/any/SonarQube.Analysis.xml
# to point to your sonar app key
# 4) install mono


# first generate the solution

/Applications/Unity/Hub/Editor/2019.3.0f3/Unity.app/Contents/MacOS/Unity \
    -batchmode \
    -logfile /dev/stdout \
    -projectPath . \
    -runTests \
    -testPlatform editmode \
    -testResults Tests/Results.xml \
    -debugCodeOptimization \
    -enableCodeCoverage \
    -coverageOptions assemblyFilters:-Mirror.Examples,-Ninja.WebSockets \
    -coverageResultsPath Tests

/Applications/Unity/Hub/Editor/2019.3.0f3/Unity.app/Contents/MacOS/Unity \
    -batchmode \
    -logfile /dev/stdout \
    -quit \
    -customBuildName buildName \
    -projectPath . \
    -buildTarget StandaloneWindows64 \
    -customBuildTarget StandaloneWindows64 \
    -customBuildPath ./build/StandaloneWindows64 \
    -executeMethod UnityEditor.SyncVS.SyncSolution


dotnet-sonarscanner begin /k:MirrorNG_MirrorNG /o:mirrorng /d:sonar.cs.nunit.reportsPaths=Tests/Results.xml /d:sonar.cs.opencover.reportsPaths=Tests/Mirror2-opencov/EditMode/TestCoverageResults_0000.xml
msbuild Mirror2.sln
dotnet-sonarscanner end