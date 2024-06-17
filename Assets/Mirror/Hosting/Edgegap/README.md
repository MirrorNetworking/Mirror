# Edgegap Unity plugin for Dedicated Servers quickstart

This plugin has been tested, and supports Unity versions 2021.2+, including all LTS releases, Unity 2023, and Unity 6.

This plugin is intended to help you:
- get started quickly with Dedicated Servers on Edgegap,
- make your server build iteration as fast as possible,
- make multiplayer development 10x easier by removing the need to learn Linux, Docker, or Cloud concepts.

This plugin does not need to be included in your builds, as it's only a development tool and does not have any runtime features.

## Install the plugin in Unity

1. Open your Unity project,
2. Select toolbar option **Window** -> **Package Manager**,
3. Click the **+** icon and select **Add package from git URL...**,
4. Input the following URL `https://github.com/edgegap/edgegap-unity-plugin.git`,
5. Click **Add** and wait for the Unity Package Manager to complete the installation.

Once you have it, check for **Edgegap** -> **Edgegap Hosting** in Unity's top menu.

From here, we recommend following our [Unity Plugin Guide](https://docs.edgegap.com/docs/tools-and-integrations/unity-plugin-guide) to get your first dedicated server deployed.

## Other sources

The only other official distribution channels for this plugin are:
- [Mirror Networking samples](https://mirror-networking.gitbook.io/docs/hosting/edgegap-hosting-plugin-guide)
- [Fish Networking samples](https://fish-networking.gitbook.io/docs/manual/server-hosting/edgegap-official-partner)

**WARNING!** The [Edgegap plugin published on Unity Asset Store](https://assetstore.unity.com/packages/tools/network/edgegap-game-server-hosting-212563) is outdated and not supported anymore. If you've previously installed our plugin by another method than described above, please remove any Edgegap files or dependencies related before updating your plugin using the git URL.

## Update the Plugin in Unity

In order to update our plugin, locate it in Unity's **Package Manager** window and click **Update**. Wait for the process to complete and you're good to go!
