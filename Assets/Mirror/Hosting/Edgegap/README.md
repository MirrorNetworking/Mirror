# Edgegap Unity plugin for Dedicated Servers quickstart

This plugin has been tested, and supports Unity versions 2021.2+, including all LTS releases, Unity 2023, and Unity 6.

This plugin is intended to help you:
- get started quickly with Dedicated Servers on Edgegap,
- make your server build iteration as fast as possible,
- make multiplayer development 10x easier by removing the need to learn Linux, Docker, or Cloud concepts.

This plugin does not need to be included in your builds, as it's only a development tool and does not have any runtime features.

## Install using git (recommended)
### Benefits
- Installing our plugin this way will ensure you get the freshest updates the moment they come out, see [the update guide](#update-the-plugin-in-unity).

### Caveats
- Requirement: functioning git client installed, for example [git-scm](https://git-scm.com/).

### Instructions
1. Open your Unity project,
2. Select toolbar option **Window** -> **Package Manager**,
3. Click the **+** icon and select **Add package from git URL...**,
4. Input the following URL `https://github.com/edgegap/edgegap-unity-plugin.git`,
5. Click **Add** and wait for the Unity Package Manager to complete the installation.

## Install via ZIP archive
### Benefits
- Slightly easier as no git client is required.

### Caveats
- Installing our plugin this way will require you to manually replace plugin contents if you [wish to update it](#update-the-plugin-in-unity),
- The newtonsoft package (dependency) version required may not be compatible with your project if you're already using an older version of this package.

### Instructions
1. Select toolbar option **Window** -> **Package Manager**,
2. Click the **+** icon and select **Add package by name...**,
3. Input the name `com.unity.nuget.newtonsoft-json` and wait for the Unity Package Manager to complete the installation.,
4. Back to this github project - make sure you're on the `main` branch,
5. Click **<> Code**, then **Download ZIP**,
6. Paste the contents of the unzipped archive in your `Assets` folder within Unity project root.

## Other sources

The only other official distribution channels for this plugin are:
- [Mirror Networking samples](https://mirror-networking.gitbook.io/docs/hosting/edgegap-hosting-plugin-guide)
- [Fish Networking samples](https://fish-networking.gitbook.io/docs/manual/server-hosting/edgegap-official-partner)

**WARNING!** The [Edgegap plugin published on Unity Asset Store](https://assetstore.unity.com/packages/tools/network/edgegap-game-server-hosting-212563) is outdated and not supported anymore. If you've previously installed our plugin by another method than described above, please remove any Edgegap files or dependencies related before updating your plugin using the git URL.

## Next Steps

Once you have it, check for **Tools** -> **Edgegap Hosting** in Unity's top menu.

From here, we recommend following our [Unity Plugin Guide](https://docs.edgegap.com/docs/tools-and-integrations/unity-plugin-guide) to get your first dedicated server deployed.

### Update the Plugin in Unity

If you've installed using git, to update our plugin, locate it in Unity's **Package Manager** window and click **Update**. Wait for the process to complete and you're good to go!

If you've installed by copying, you will have to remove the Edgegap folder and paste the newer copy. Your settings are saved in your Unity version, so they shouldn't be lost in this process.
