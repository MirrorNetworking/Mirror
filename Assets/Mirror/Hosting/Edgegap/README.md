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

- [Unity Asset Store package](https://assetstore.unity.com/packages/tools/network/edgegap-game-server-hosting-212563)
- [Mirror Networking source](https://github.com/MirrorNetworking/Mirror)
- [Mirror Networking free package](https://assetstore.unity.com/packages/tools/network/mirror-129321)
- [Mirror Networking LTS package](https://assetstore.unity.com/packages/tools/network/mirror-lts-102631)
- [Fish Networking source](https://github.com/FirstGearGames/FishNet)
- [Fish Networking free package](https://assetstore.unity.com/packages/tools/network/fishnet-networking-evolved-207815)
- [Fish Networking Pro package](https://assetstore.unity.com/packages/tools/network/fishnet-pro-networking-evolved-287711)

## Next Steps

Once you have it, check for **Tools** -> **Edgegap Hosting** in Unity's top menu.

### Usage requirements

To take full advantage of our Unity plugin's build features, you will need to:

- [Create an Edgegap Free Tier account](https://app.edgegap.com/auth/register),
- [Install Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker CLI),
- Install Unity Linux Build Support modules for Unity.

From here, we recommend following our [Unity Plugin Guide](https://docs.edgegap.com/docs/tools-and-integrations/unity-plugin-guide) to get your first dedicated server deployed.

### Update the Plugin in Unity

If you've installed using git, to update our plugin, locate it in Unity's **Package Manager** window and click **Update**. Wait for the process to complete and you're good to go!

If you've installed by copying, you will have to remove the Edgegap folder and paste the newer copy. Your settings are saved in your Unity version, so they shouldn't be lost in this process.

## For plugin developers

This section is only for developers working on this plugin or other plugins interacting / integrating this plugin.

### CSharpier code formatter

This project uses [CSharpier code formatter](https://csharpier.com/) to ensure consistent and readable formatting, configured in `/.config/dotnet-tools.json`.

See [Editor integration](https://csharpier.com/docs/Editors) for Visual Studio extensions, optionally configure `Reformat with CSharpier` on Save under Tools | Options | CSharpier | General. You may also configure [running formatting as a pre-commit git hook](https://csharpier.com/docs/Pre-commit).

### Compiler Symbols

If you wish to detect presence of this plugin in the users Unity Editor, you can do so using a compiler directive:

```csharp
#if EDGEGAP_PLUGIN_SERVERS
{...your code...}
#endif
```
