# HLAPI Community Edition

[![Build status](https://img.shields.io/appveyor/ci/vis2k/hlapi-community-edition/features.svg)](https://ci.appveyor.com/project/vis2k/hlapi-community-edition/branch/features)
[![AppVeyor tests branch](https://img.shields.io/appveyor/tests/vis2k/hlapi-community-edition/features.svg)](https://ci.appveyor.com/project/vis2k/hlapi-community-edition/branch/features/tests)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discord.gg/wvesC6)
[![Codecov](https://codecov.io/bb/vis2k/hlapi-community-edition/branch/features/graph/badge.svg)](https://codecov.io/bb/vis2k/hlapi-community-edition/branch/features)

Unity is the best game engine in the world, which should make UNET the best multiplayer game development solution in the world, but it's not.

UNET consists of two parts:

* The LLAPI is developed by [@aabramychev](https://forum.unity.com/members/aabramychev.118911/) and deserves more credit than it gets. It's nothing short of amazing. We all love your work Alex!
* The HLAPI was developed by [Sean Riley](https://forum.unity.com/members/691722/) as an example to showcase the LLAPI. Sean Riley left Unity and everyone's hopes for HLAPI improvements remained mostly (not entirely) unanswered.

UNET's HLAPI was abandoned a long time ago when Sean Riley left unity.

UNET's HLAPI was made [open source](https://bitbucket.org/Unity-Technologies/networking) shortly after.

This project is a fork of HLAPI and picks up where HLAPI stopped. 
A lot of bugs have been fixed while maintaining compatibility.
We have refactored to improve code quality and reduce complexity.
We are also adding new features useful for developers.

# Download:

* [HLAPI Community Edition 2017.1](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_fixesonly_Unity_2017.1.zip)
* [HLAPI Community Edition 2017.2](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_fixesonly_Unity_2017.2.zip)
* [HLAPI Community Edition 2017.3](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_fixesonly_Unity_2017.3.zip)
* [HLAPI Community Edition 2017.4](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_Unity_2017.4.zip)
* [HLAPI Community Edition 2017.4_improvements](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_Unity_2017.4_improvements.zip) <- **Recommended**
* [HLAPI Community Edition 2018.1](http://noobtuts.com/_projects/HLAPI-Pro/HLAPI_Pro_Unity_2018.1.zip)


Use at your own risk. In case of concerns, feel free to inspect the .DLL files with ILSpy!

# Build:

To build this project,  clone the repository,  open Networking.sln it in visual studio 2017 and build all projects.

# Installation:
Backup the original DLL files from your Unity installation folder and replace them with the HLAPI Community Edition DLL files:

## Windows:
1. *Backup your Project! Be wise now and don't lose everything if something unexpected happens.*
2. replace C:\Program Files\Unity\Editor\Data\UnityExtensions\Unity\Networking content with the downloaded files.
3. move the Unity.UNetWeaver.dll to C:\Program Files\Unity\Editor\Data\Managed
4. Restart Unity for the UNetWeaver.dll to be reloaded properly. Rebuild all your clients/servers so they use the same DLLs.
5. Rebuild your server/client.exe so that it uses the DLL as too.
## Mac:
1. *Backup your Project! Be wise now and don't lose everything if something unexpected happens.*
2. replace Unity.app/Contents/UnityExtensions/Unity/Networking/ content with the downloaded files
3. move the Unity.UNetWeaver.dll to Unity.app/Contents/Managed/
4. Restart Unity for the UNetWeaver.dll to be reloaded properly. Rebuild all your clients/servers so they use the same DLLs.
5. Rebuild your server/client.app so that it uses the DLL as too.

Note: right click Unity.app and select 'Show Package Contents' to see the subfolders.
## Linux:
You'll find it  
(feel free to submit a pull request for filling this part)

# Branches:

We have multiple yet sometimes conflicting goals. Thus we are developing HLAPI in several branches:

* [fixes](https://bitbucket.org/vis2k/hlapi-community-edition/src/fixes/): 2017.4 HLAPI + bug fixes. No unnecessary code changes to guarantee 100% compatibility with original HLAPI, for those who need it.
* [2018.1](https://bitbucket.org/vis2k/hlapi-community-edition/src/2018.1/): 2018.1 patch. Can be rebased to latest 'master' all the time.
* [improvements](https://bitbucket.org/vis2k/hlapi-community-edition/src/improvements/): the #1 goal of this branch is to make HLAPI more simple and easier to maintain. The original code is way too complicated and if we end up with 10.000 lines instead of 20.000 lines, then that would be huge. The #2 goal of this branch is to improve CCU and only add features that are completely obviously necessary (SyncVarToOwner etc.).
* [features](https://bitbucket.org/vis2k/hlapi-community-edition/src/features/): this branch is for new features that could be useful. We can go crazy with features here, as long as we all agree that a given feature is a good idea to add. We can discuss features in Discord.


If you submit pull requests, please submit them to the proper branch. 
For example, 99% of the features submitted to 'improvements' will most likely be rejected, 
because the goal is to make this branch more simple. 
Submit new features to 'features' branch instead. 
If you want to submit a bug fix that applies to everything, then submit it to 'master', and so on.

