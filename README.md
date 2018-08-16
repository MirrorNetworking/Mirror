# HLAPI Community Edition

[![Build status](https://img.shields.io/appveyor/ci/vis2k73562/hlapi-community-edition/Improvements.svg)](https://ci.appveyor.com/project/vis2k73562/hlapi-community-edition/branch/improvements)
[![AppVeyor tests branch](https://img.shields.io/appveyor/tests/vis2k73562/hlapi-community-edition/Improvements.svg)](https://ci.appveyor.com/project/vis2k73562/hlapi-community-edition/branch/improvements/tests)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discordapp.com/invite/N9QVxbM)
[![Codecov](https://codecov.io/gh/vis2k/hlapi-community-edition/branch/improvements/graph/badge.svg)](https://codecov.io/gh/vis2k/hlapi-community-edition/branch/improvements)

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
*NOTE:* Assuming you installed the experimental Unity build in your home directory at, let's say `/home/user/Unity-2017.4.8`. This was also tested on a version of the Ubuntu-based KDE Neon distro.
1. *Backup your Project! Be wise now and don't lose everything if something unexpected happens.* Unity is experimental on Linux and **THIS COULD SPELL DOOM FOR YOUR PROJECT** if the Editor breaks in half. (Probably not, though.)
2. The `Editor` and `Standalone` folders go into `/home/user/Unity-2017.4.8/Editor/Data/UnityExtensions/Unity/Networking`. If prompted for overwrite, allow the overwrite.
3. If you do not have this directory path, *STOP*. Submit a support request and we'll try to help you out.
4. Copy `Unity.UNetWeaver.dll` into `/home/user/Unity-2017.4.8/Editor/Data/Managed`.
5. (Re-)Start Unity to make sure the changes are applied. Be patient while Unity recompiles your network scripts for use with the latest version of HLAPI CE.
6. If you got no critical/fatal errors from Unity itself then *congratulations*. If you get errors from your scripts, it's likely due to stuff that got stripped out in the improvements branch. Consider updating your code.
7. Again, it should be noted that Unity on Linux is very experimental and likely to blow up at any given moment. While it does work, we cannot vouch for its stability on the Linux platform.
8. Rebuild your server and client builds to properly use the features and improvements in HLAPI CE.

# Branches:

We have multiple yet sometimes conflicting goals. Thus we are developing HLAPI in several branches:

* [fixes](https://github.com/vis2k/HLAPI-Community-Edition/tree/fixes): 2017.4 HLAPI + bug fixes. No unnecessary code changes to guarantee 100% compatibility with original HLAPI, for those who need it.
* [2018.1](https://github.com/vis2k/HLAPI-Community-Edition/tree/2018.1): 2018.1 patch. Can be rebased to latest 'master' all the time.
* [improvements](https://github.com/vis2k/HLAPI-Community-Edition/tree/improvements): the #1 goal of this branch is to make HLAPI more simple and easier to maintain. The original code is way too complicated and if we end up with 10.000 lines instead of 20.000 lines, then that would be huge. The #2 goal of this branch is to improve CCU and only add features that are completely obviously necessary (SyncVarToOwner etc.).
* [features](https://github.com/vis2k/HLAPI-Community-Edition/tree/features): this branch is for new features that could be useful. We can go crazy with features here, as long as we all agree that a given feature is a good idea to add. We can discuss features in Discord.


If you submit pull requests, please submit them to the proper branch. 
For example, 99% of the features submitted to 'improvements' will most likely be rejected, 
because the goal is to make this branch more simple. 
Submit new features to 'features' branch instead. 
If you want to submit a bug fix that applies to everything, then submit it to 'fixes', and so on.

