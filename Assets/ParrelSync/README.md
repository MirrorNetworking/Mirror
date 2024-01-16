# ParrelSync 
[![Release](https://img.shields.io/github/v/release/VeriorPies/ParrelSync?include_prereleases)](https://github.com/VeriorPies/ParrelSync/releases) [![Documentation](https://img.shields.io/badge/documentation-brightgreen.svg)](https://github.com/VeriorPies/ParrelSync/wiki) [![License](https://img.shields.io/badge/license-MIT-green)](https://github.com/VeriorPies/ParrelSync/blob/master/LICENSE.md) [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-blue.svg)](https://github.com/VeriorPies/ParrelSync/pulls) [![Chats](https://img.shields.io/discord/710688100996743200)](https://discord.gg/TmQk2qG)  

ParrelSync is a Unity editor extension that allows users to test multiplayer gameplay without building the project by having another Unity editor window opened and mirror the changes from the original project.

<br>

![ShortGif](https://raw.githubusercontent.com/VeriorPies/ParrelSync/master/Images/Showcase%201.gif)
<p align="center">
<b>Test project changes on clients and server within seconds - both in editor
</b>
<br>
</p>

## Features
1. Test multiplayer gameplay without building the project
2. GUI tools for managing all project clones
3. Protected assets from being modified by other clone instances
4. Handy APIs to speed up testing workflows
## Installation

1. Backup your project folder or use a version control system such as [Git](https://git-scm.com/) or [SVN](https://subversion.apache.org/)
2. Download .unitypackage from the [latest release](https://github.com/VeriorPies/ParrelSync/releases) and import it to your project. 
3.  ParrelSync should appreared in the menu item bar after imported
![UpdateButtonInMenu](https://github.com/VeriorPies/ParrelSync/raw/master/Images/AfterImported.png)  

Check out the [Installation-and-Update](https://github.com/VeriorPies/ParrelSync/wiki/Installation-and-Update) page for more details.

### UPM Package
ParrelSync can also be installed via UPM package.  
After Unity 2019.3.4f1, Unity 2020.1a21, which support path query parameter of git package. You can install ParrelSync by adding the following to Package Manager.

```
https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync
```  

  
![UPM_Image](https://github.com/VeriorPies/ParrelSync/raw/master/Images/UPM_1.png?raw=true) ![UPM_Image2](https://github.com/VeriorPies/ParrelSync/raw/master/Images/UPM_2.png?raw=true)
  
or by adding 

```
"com.veriorpies.parrelsync": "https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync"
``` 

to the `Packages/manifest.json` file 


## Supported Platform
Currently, ParrelSync supports Windows, macOS and Linux editors.  

ParrelSync has been tested with the following Unity version. However, it should also work with other versions as well.
* *2020.3.1f1*
* *2019.3.0f6*
* *2018.4.22f1*


## APIs
There's some useful APIs for speeding up the multiplayer testing workflow.
Here's a basic example: 
```
if (ClonesManager.IsClone()) {
  // Automatically connect to local host if this is the clone editor
}else{
  // Automatically start server if this is the original editor
}
```
Check out [the doc](https://github.com/VeriorPies/ParrelSync/wiki/List-of-APIs) to view the complete API list.

## How does it work?
For each clone instance, ParrelSync will make a copy of the original project folder and reference the ```Asset```, ```Packages``` and ```ProjectSettings``` folder back to the original project with [symbolic link](https://docs.microsoft.com/en-us/windows-server/administration/windows-commands/mklink). Other folders such as ```Library```, ```Temp```, and ```obj``` will remain independent for each clone project.

All clones are placed right next to the original project with suffix *```_clone_x```*, which will be something like this in the folder hierarchy. 
```
/ProjectName
/ProjectName_clone_0
/ProjectName_clone_1
...
```
## Discord Server
We have a [Discord server](https://discord.gg/TmQk2qG).

## Need Help?
Some common questions and troubleshooting can be found under the [Troubleshooting & FAQs](https://github.com/VeriorPies/ParrelSync/wiki/Troubleshooting-&-FAQs) page.  
You can also [create a question post](https://github.com/VeriorPies/ParrelSync/issues/new/choose), or ask on [Discord](https://discord.gg/TmQk2qG) if you prefer to have a real-time conversation.

## Support this project 
A star will be appreciated :)

## Credits
This project is originated from hwaet's [UnityProjectCloner](https://github.com/hwaet/UnityProjectCloner)
