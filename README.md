![Mirror Logo](https://i.imgur.com/we6li1x.png)

[![Documentation](https://img.shields.io/badge/documentation-brightgreen.svg)](https://mirrorng.github.io/MirrorNG/)
[![Forum](https://img.shields.io/badge/forum-brightgreen.svg)](https://forum.unity.com/threads/mirror-networking-for-unity-aka-hlapi-community-edition.425437/)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discordapp.com/invite/N9QVxbM)
[![release](https://img.shields.io/github/release/MirrorNG/MirrorNG.svg)](https://github.com/MirrorNG/MirrorNG/releases/latest)
[![openupm](https://img.shields.io/npm/v/com.mirrorng.mirrorng?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.mirrorng.mirrorng/)

[![Build](https://github.com/MirrorNG/MirrorNG/workflows/CI/badge.svg)](https://github.com/MirrorNG/MirrorNG/actions?query=workflow%3ACI)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=alert_status)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)
[![SonarCloud Coverage](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=coverage)](https://sonarcloud.io/component_measures?id=MirrorNG_MirrorNG&metric=coverage)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=ncloc)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=sqale_index)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=code_smells)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)


MirrorNG is a **high level** Networking API for Unity.

MirrorNG is built [and tested](https://www.youtube.com/watch?v=mDCNff1S9ZU) for **MMO Scale** Networking by the developers of  [Cubica](https://cubica.net).

MirrorNG is optimized for **ease of use** and **probability of success**.

With MirrorNG the objects in the client are mirror images of the objects in the server.  MirrorNG provides all the tools necessary to keep them in sync and pass messages between them.

## Architecture
The **Server & Client** are **ONE project** in order to achieve an order of magnitude gain in productivity.

## Comparison with Mirror
When migrating a project from Mirror to MirrorNG, these will be the most notable differences.

| MirrorNG                                      | Mirror                                 |
| --------------------------------------------- | -------------------------------------- |
| Install via Unity Package Manager             | Install from Asset Store               |
| Errors are thrown as exceptions               | Errors are logged                      |
| `[ServerRpc]`                                 | `[Command]`                            |
| `[ClientRpc(target=Client.Owner)]`            | `[TargetRpc]`                          |
| Subscribe to events in `NetworkServer`        | Override methods in `NetworkManager`   |
| Subscribe to events in `NetworkClient`        | Override methods in `NetworkManager`   |
| Subscribe to events in `NetworkIdentity`      | Override methods in `NetworkBehaviour` |
| Methods use PascalCase (C# guidelines)        | No consistency                         |
| `NetworkTime` available in `NetworkBehaviour` | `NetworkTime` is global static         |
| Send any data as messages                     | Messages must implement NetworkMessage |
| Supports Unity 2019.3 or later                | Supports Unity 2018.4 or later         |

MirrorNG has many new features
* MirrorNG supports [fast domain reload](https://blogs.unity3d.com/2019/11/05/enter-play-mode-faster-in-unity-2019-3/)
* Components can be added in child objects
* Your client can connect to multiple servers. For example chat server and game server
* Modular,  use only the components you need.
* Error handling
* [Version defines](https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html#define-symbols)
* Server Rpcs can [return values](https://mirrorng.github.io/MirrorNG/Articles/Guides/Communications/RemoteActions.html)

If you look under the hood,  the code base has some significant differences based on the core values of each project
* MirrorNG follows the [SOLID principles](https://en.wikipedia.org/wiki/SOLID).
* MirrorNG avoids singletons and static state in general.
* MirrorNG has high [![Test Coverage](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=coverage)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)
* MirrorNG has low [![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=sqale_index)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)
* MirrorNG values code quality,  Mirror values API stability

## Installation
If you want to make a game with MirrorNG, the preferred installation method is Unity Package Manager.

### Install from git url
Use unity 2019.3 or later. 

1) Install [git](https://www.git-scm.com/)
2) Open your project in unity
3) Install [UniTask](https://github.com/Cysharp/UniTask) using Unity Package Manager
3) Click on Windows -> Package Manager
4) Click on the plus sign on the left and click on "Add package from git URL..."
5) enter https://github.com/MirrorNG/MirrorNG.git?path=/Assets/Mirror
6) Unity will download and install MirrorNG
7) Set a Scoped Register to see updates

![Scoped Registry](https://i.imgur.com/zr6vjbk.png)

### Install using [openupm](https://openupm.com/packages/com.mirrorng.mirrorng/)
This is how I do it for Cubica because unity will display all versions of MirrorNG and allow me to switch amongst them.

1) Install [git](https://www.git-scm.com/)
2) Install [node.js 12](https://nodejs.org/en/)
3) Install [openupm](https://openupm.com/)
4) install MirrorNG in your project:
    ```sh
    cd YOUR_PROJECT
    openupm add com.mirrorng.mirrorng 
    ```
5) Open your project in Unity 

### Install manually
If you prefer some pain, you can download it directly from the [release section](https://github.com/MirrorNG/MirrorNG/releases) and add it to your project. You will need to manually install UniTask and Cecil.

## Development environment
If you want to contribute to  MirrorNG, follow these steps:

### Linux and Mac
1) Install git
2) clone this repo
3) Open in unity 2019.4.x or later

### Windows
1) Install [git](https://git-scm.com/download/win) or use your favorite git client
2) as administrator, clone this repo with symbolic links support:
    ```sh
    git clone -c core.symlinks=true https://github.com/MirrorNG/MirrorNG.git
    ```
    It you don't want to use administrator, [add symlink support](https://www.joshkel.com/2018/01/18/symlinks-in-windows/) to your account.
    If you don't enable symlinks, you will be able to work on MirrorNG but Unity will not see the examples.
3) Open in unity 2019.4.x or later

## Transports
Here is a list of some transports supported by NG and how they compare to each other

|                |        Kcp         | [Websocket](https://github.com/MirrorNG/WebsocketNG) | [Steam](https://github.com/dragonslaya84/FizzySteamyMirror) | [LiteNetLibNG](https://github.com/uweenukr/LiteNetLibNG) | [IgnoranceNG](https://github.com/dragonslaya84/IgnoranceNG) |
| -------------- | :----------------: | :--------------------------------------------------: | :---------------------------------------------------------: | :------------------------------------------------------: | :---------------------------------------------------------: |
| **CCU**        |       1000+        |                          ?                           |                              ?                              |                            ?                             |                              ?                              |
| **Protocol**   |        UDP         |                         TCP                          |                             UDP                             |                           UDP                            |                             UDP                             |
| **Unreliable** | :white_check_mark: |                                                      |                     :white_check_mark:                      |                    :white_check_mark:                    |                     :white_check_mark:                      |
| **WebGL**      |                    |                  :white_check_mark:                  |                                                             |                                                          |                                                             |
| **Mobile**     | :white_check_mark: |                                                      |                                                             |                            ?                             |                              ?                              |
| **CPU**        |        LOW         |                         HIGH                         |                              ?                              |                            ?                             |                              ?                              |
| **NAT Punch**  |                    |                                                      |                     :white_check_mark:                      |                                                          |                                                             |
| **Encryption** |                    |                  :white_check_mark:                  |                     :white_check_mark:                      |                                                          |                                                             |
| **IPV6**       | :white_check_mark: |                  :white_check_mark:                  |                              ?                              |                            ?                             |                              ?                              |
| **Managed**    | :white_check_mark: |                  :white_check_mark:                  |                                                             |                    :white_check_mark:                    |                                                             |
| **Based on**   |        KCP         |                      Websockets                      |                Steam Game Networking Sockets                |                        LiteNetLib                        |                            ENet                             |

## Contributing

There are several ways to contribute to this project:

* Pull requests for bug fixes and features are always appreciated.
* Pull requests to improve the documentation is also welcome
* Make tutorials on how to use this
* Test it and open issues
* Review existing pull requests
* Donations

When contributing code, please keep these things in mind:

* [KISS](https://en.wikipedia.org/wiki/KISS_principle) principle. Everything needs to be **as simple as possible**. 
* An API is like a joke,  if you have to explain it is not a good one.  Do not require people to read the documentation if you can avoid it.
* Follow [C# code conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions).
* Follow [SOLID principles](https://en.wikipedia.org/wiki/SOLID) as much as possible. 
* Keep your pull requests small and obvious,  if a PR can be split into several small ones, do so.

