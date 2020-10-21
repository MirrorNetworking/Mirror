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

| MirrorNG                                                                                      | Mirror                                 |
| --------------------------------------------------------------------------------------------- | -------------------------------------- |
| Install via Unity Package Manager                                                             | Install from Asset Store               |
| [Domain Reload](https://blogs.unity3d.com/2019/11/05/enter-play-mode-faster-in-unity-2019-3/) |                                        |
| Errors are thrown as exceptions                                                               | Errors are logged                      |
| `[ServerRpc]`                                                                                 | `[Command]`                            |
| `[ClientRpc(target=Client.Owner)]`                                                            | `[TargetRpc]`  ass of Synclist         |
| Subscribe to events in `NetworkServer`                                                        | Override methods in `NetworkManager`   |
| Subscribe to events in `NetworkClient`                                                        | Override methods in `NetworkManager`   |
| Subscribe to events in `NetworkIdentity`                                                      | Override methods in `NetworkBehaviour` |
| Methods use PascalCase (C# guidelines)                                                        | No consistency                         |
| `NetworkTime` available in `NetworkBehaviour`                                                 | `NetworkTime` is global static         |
| Send any data as messages                                                                     | Messages must implement NetworkMessage |
| Supports Unity 2019.3 or later                                                                | Supports Unity 2018.4 or later         |
| Components can be added in child objects                                                      | Components must be added at root level |

If you look under the hood,  the code base has some significant diferences based on the core values of each project
* MirrorNG tries to adhere to the [SOLID principles](https://en.wikipedia.org/wiki/SOLID).
* Mirror uses singletons.  MirrorNG avoids singletons and static state in general.
* MirrorNG has better  [![Test Coverage](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=coverage)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)
* MirrorNG has much lower [![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_MirrorNG&metric=sqale_index)](https://sonarcloud.io/dashboard?id=MirrorNG_MirrorNG)
* MirrorNG values code quality,  Mirror values API stability

## Installation
If you want to make a game with MirrorNG, the preferred installation method is Unity Package manager.

If you are using unity 2019.3 or later: 

1) Install [git](https://www.git-scm.com/)
2) Open your project in unity
3) Click on Windows -> Package Manager
4) Click on the plus sign on the left and click on "Add package from git URL..."
5) enter https://github.com/MirrorNG/MirrorNG.git?path=/Assets/Mirror
6) Unity will download and install MirrorNG

If you are using unity 2019.2, you can use [openupm](https://openupm.com/packages/com.mirrorng.mirrorng/) or you can manually add the url to your [packages.json](https://docs.unity3d.com/Manual/upm-git.html) file. 

Alternatively you can download it from [Download Mirror](https://github.com/MirrorNG/MirrorNG/releases).  You will need to install some dependencies yourself such as cecil.

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

|                |        Tcp         |        Kcp         | [Websocket](https://github.com/MirrorNG/WebsocketNG) | [Steam](https://github.com/dragonslaya84/FizzySteamyMirror) | [LiteNetLibNG](https://github.com/uweenukr/LiteNetLibNG) | [IgnoranceNG](https://github.com/dragonslaya84/IgnoranceNG) |
| -------------- | :----------------: | :----------------: | :--------------------------------------------------: | :---------------------------------------------------------: | :------------------------------------------------------: | :---------------------------------------------------------: |
| **CCU**        |        100         |       1000+        |                          ?                           |                              ?                              |                            ?                             |                              ?                              |
| **Protocol**   |        TCP         |        UDP         |                         TCP                          |                             UDP                             |                           UDP                            |                             UDP                             |
| **Unreliable** |                    | :white_check_mark: |                                                      |                     :white_check_mark:                      |                    :white_check_mark:                    |                     :white_check_mark:                      |
| **WebGL**      |                    |                    |                  :white_check_mark:                  |                                                             |                                                          |                                                             |
| **Mobile**     | :white_check_mark: | :white_check_mark: |                                                      |                                                             |                            ?                             |                              ?                              |
| **CPU**        |        HIGH        |        LOW         |                         HIGH                         |                              ?                              |                            ?                             |                              ?                              |
| **NAT Punch**  |                    |                    |                                                      |                     :white_check_mark:                      |                                                          |                                                             |
| **Encryption** |                    |                    |                  :white_check_mark:                  |                     :white_check_mark:                      |                                                          |                                                             |
| **IPV6**       | :white_check_mark: | :white_check_mark: |                  :white_check_mark:                  |                              ?                              |                            ?                             |                              ?                              |
| **Managed**    | :white_check_mark: | :white_check_mark: |                  :white_check_mark:                  |                                                             |                    :white_check_mark:                    |                                                             |
| **Based on**   |   Async Sockets    |        KCP         |                   Ninja Websockets                   |                Steam Game Networking Sockets                |                        LiteNetLib                        |                            ENet                             |

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

