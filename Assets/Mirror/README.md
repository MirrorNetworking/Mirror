# Mirror Networking

![Mirror Logo](https://user-images.githubusercontent.com/16416509/119120944-6db26780-ba5f-11eb-9cdd-fc8500207f4d.png)

**Mirror** is a **high-level** networking library for **Unity**, designed to make multiplayer games simple and accessible.

## Overview

Mirror is the #1 free open source game networking library for Unity 2019/2020/2021/2022/6. Used in production by major hits like **Population: ONE** and many more.

Originally based on **UNET**: battle tested since 2014 for 10 years and counting!

Mirror is **stable**, **modular** & **easy to use** for all types of games, even small **MMORPGs**.

## Features

- 🎛 **Multiple Transports**: UDP, TCP, Websockets, Steam, Relay and more
- 🪜 **Interest Management**: Spatial Hashing & Distance Checker to partition the world
- ↗️ **SyncDirection**: Server & Client Authority - per component with one click
- 🐌 **Latency Simulation**: Simulate latency, packet loss & jitter locally
- 🧲 **Batching**: Minimize message overhead via automatic batching
- 💌 **RPCs & SyncVars**: Synced vars and remote function calls built in & safe
- 🙅‍♀️ **Allocation Free**: Free of runtime allocations and no GC (except Transports)
- 🛞 **Transform & Physics**: Transform & Physics sync built in
- 👩‍🍼 **Child Components**: Put networked components on anything
- 🪚️ **IL Post Processing**: Zero overhead [Rpcs] and [Commands] via IL post processing
- 📏 **Snapshot Interpolation**: Perfectly smooth movement for all platforms & genres

## Installation

### via Unity Package Manager (UPM)

1. Open Unity Package Manager (Window > Package Manager)
2. Click the '+' button in the top-left corner
3. Select "Add package from git URL..."
4. Enter the following URL:
   ```
   https://github.com/MirrorNetworking/Mirror.git?path=/Assets/Mirror
   ```

### via manifest.json

Add this to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.mirror-networking.mirror": "https://github.com/MirrorNetworking/Mirror.git?path=/Assets/Mirror"
  }
}
```

## Quick Start

### Basic NetworkBehaviour Example

```csharp
using Mirror;
using UnityEngine;

public class Player : NetworkBehaviour
{
    // Synced automatically
    [SyncVar] public int health = 100;
    
    // Lists, Dictionaries, Sets too
    SyncList<Item> inventory = new SyncList<Item>();
    
    // Server/Client-only code
    [Server] void LevelUp() {}
    [Client] void Animate() {}
    
    void Update()
    {
        // isServer/isClient for runtime checks
        if (isServer) Heal();
        if (isClient) Move();
    }
    
    // Zero overhead remote calls
    [Command]   void CmdUseItem(int slot) {} // Client to Server
    [ClientRpc] void RpcRespawn() {}         // Server to all Clients
    [TargetRpc] void Hello() {}              // Server to one Client
}
```

## Documentation

- **Documentation**: https://mirror-networking.gitbook.io/
- **API Reference**: https://mirror-networking.com/docs/api/
- **Forum**: https://forum.unity.com/threads/mirror-networking-for-unity-aka-hlapi-community-edition.425437/
- **Discord**: https://discord.gg/xVW4nU4C34

## Support

- **Discord Community**: https://discord.gg/xVW4nU4C34 (14,000+ developers)
- **GitHub Issues**: https://github.com/MirrorNetworking/Mirror/issues
- **Forum**: https://forum.unity.com/threads/mirror-networking-for-unity-aka-hlapi-community-edition.425437/

## Requirements

- Unity 2019.4 LTS or later
- .NET 4.x Runtime (Project Settings > Player > Other Settings)

## License

MIT License - See [LICENSE](https://github.com/MirrorNetworking/Mirror/blob/master/LICENSE) file for details.

## Credits

Made with ❤️ by the Mirror Networking team and community.

**Made in 🇩🇪🇺🇸🇬🇧🇸🇬🇹🇼**
