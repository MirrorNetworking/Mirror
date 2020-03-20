## [20.0.1](https://github.com/MirrorNG/MirrorNG/compare/20.0.0-master...20.0.1-master) (2020-03-20)


### Bug Fixes

* NRE when destroying all objects ([#85](https://github.com/MirrorNG/MirrorNG/issues/85)) ([71e78a7](https://github.com/MirrorNG/MirrorNG/commit/71e78a7f5e15f99af89cd15c1ddd8a975e463916))

# [20.0.0](https://github.com/MirrorNG/MirrorNG/compare/19.1.2-master...20.0.0-master) (2020-03-20)


### Bug Fixes

* compilation issue after merge from mirror ([daf07be](https://github.com/MirrorNG/MirrorNG/commit/daf07bea83c9925bd780e23de53dd50604e8a999))


* merge clientscene and networkclient (#84) ([dee1046](https://github.com/MirrorNG/MirrorNG/commit/dee10460325119337401dc4d237dec8bfb9ddb29)), closes [#84](https://github.com/MirrorNG/MirrorNG/issues/84)


### Features

* SyncSet and SyncDictionary now show in inspector ([#1561](https://github.com/MirrorNG/MirrorNG/issues/1561)) ([5510711](https://github.com/MirrorNG/MirrorNG/commit/55107115c66ea38b75edf4a912b5cc48351128f7))


### BREAKING CHANGES

* ClientScene is gone

## [19.1.2](https://github.com/MirrorNG/MirrorNG/compare/19.1.1-master...19.1.2-master) (2020-03-20)


### Bug Fixes

* examples now work with prefabs in NC ([df4149b](https://github.com/MirrorNG/MirrorNG/commit/df4149b8fea9f174742d20f600ef5261058e5300))

## [19.1.1](https://github.com/MirrorNG/MirrorNG/compare/19.1.0-master...19.1.1-master) (2020-03-20)


### Bug Fixes

* Fixed ClienRpc typos ([e946c79](https://github.com/MirrorNG/MirrorNG/commit/e946c79194dd9618992a4136daed4b79f60671a2))
* Prevent Double Call of NetworkServer.Destroy ([#1554](https://github.com/MirrorNG/MirrorNG/issues/1554)) ([2d1b142](https://github.com/MirrorNG/MirrorNG/commit/2d1b142276193be1e93a3a3f6ce482c87a134a2c))
* show private serializable fields in network behavior inspector ([#1557](https://github.com/MirrorNG/MirrorNG/issues/1557)) ([b8c87d9](https://github.com/MirrorNG/MirrorNG/commit/b8c87d9053e7fd7c3b69bcf1d4179e6e4c9bc4a6))
* Updated NetworkRoomPlayer inspector and doc and image ([a4ffcbe](https://github.com/MirrorNG/MirrorNG/commit/a4ffcbe280e2e2318d7cd2988379af74f0d32021))

# [19.1.0](https://github.com/MirrorNG/MirrorNG/compare/19.0.1-master...19.1.0-master) (2020-03-19)


### Features

* Now you can pass NetworkIdentity and GameObjects ([#83](https://github.com/MirrorNG/MirrorNG/issues/83)) ([dca2d40](https://github.com/MirrorNG/MirrorNG/commit/dca2d4056fe613793480b378d25509284a1fd46a))

## [19.0.1](https://github.com/MirrorNG/MirrorNG/compare/19.0.0-master...19.0.1-master) (2020-03-17)


### Bug Fixes

* calling syncvar hook when not connected yet ([#77](https://github.com/MirrorNG/MirrorNG/issues/77)) ([e64727b](https://github.com/MirrorNG/MirrorNG/commit/e64727b74bcbb1adfcd8f5efbf96066443254dff))

# [19.0.0](https://github.com/MirrorNG/MirrorNG/compare/18.0.0-master...19.0.0-master) (2020-03-17)


* removed obsoletes (#1542) ([4faec29](https://github.com/MirrorNG/MirrorNG/commit/4faec295593b81a709a57aaf374bb5b080a04538)), closes [#1542](https://github.com/MirrorNG/MirrorNG/issues/1542)


### BREAKING CHANGES

* removed obsoletes

# [18.0.0](https://github.com/MirrorNG/MirrorNG/compare/17.0.2-master...18.0.0-master) (2020-03-17)


### Features

* Time sync is now done per NetworkClient ([b24542f](https://github.com/MirrorNG/MirrorNG/commit/b24542f62c6a2d0c43588af005f360ed74c619ca))


### BREAKING CHANGES

* NetworkTime.Time is no longer static

## [17.0.2](https://github.com/MirrorNG/MirrorNG/compare/17.0.1-master...17.0.2-master) (2020-03-17)


### Bug Fixes

* Command and Rpc debugging information ([#1551](https://github.com/MirrorNG/MirrorNG/issues/1551)) ([658847b](https://github.com/MirrorNG/MirrorNG/commit/658847b096571eb7cf14e824ea76359576121e63)), closes [#1550](https://github.com/MirrorNG/MirrorNG/issues/1550)

## [17.0.1](https://github.com/MirrorNG/MirrorNG/compare/17.0.0-master...17.0.1-master) (2020-03-15)


### Bug Fixes

* Report correct channel to profiler in SendToObservers ([0b84d4c](https://github.com/MirrorNG/MirrorNG/commit/0b84d4c5e1b8455e32eeb4d4c00b60bbc1301436))

# [17.0.0](https://github.com/MirrorNG/MirrorNG/compare/16.0.0-master...17.0.0-master) (2020-03-15)


### Code Refactoring

* observers is now a set of connections ([#74](https://github.com/MirrorNG/MirrorNG/issues/74)) ([4848920](https://github.com/MirrorNG/MirrorNG/commit/484892058b448012538754c4a1f2eac30a42ceaa))


### BREAKING CHANGES

* observers is now a set of connections, not a dictionary

# [16.0.0](https://github.com/MirrorNG/MirrorNG/compare/15.0.7-master...16.0.0-master) (2020-03-10)


### Code Refactoring

*  Client and server keep their own spawned list ([#71](https://github.com/MirrorNG/MirrorNG/issues/71)) ([c2599e2](https://github.com/MirrorNG/MirrorNG/commit/c2599e2c6157dd7539b560cd4645c10713a39276))


### BREAKING CHANGES

* cannot pass GameObjects and NetworkIdentities anymore.
Will be restored in the future.

## [15.0.7](https://github.com/MirrorNG/MirrorNG/compare/15.0.6-master...15.0.7-master) (2020-03-10)


### Bug Fixes

* Use big endian for packet size ([1ddcbec](https://github.com/MirrorNG/MirrorNG/commit/1ddcbec93509e14169bddbb2a38a7cf0d53776e4))

## [15.0.6](https://github.com/MirrorNG/MirrorNG/compare/15.0.5-master...15.0.6-master) (2020-03-09)


### Bug Fixes

* compilation issues ([22bf925](https://github.com/MirrorNG/MirrorNG/commit/22bf925f1ebf018b9ea33df22294fb9205574fa5))
* NetworkBehaviour.SyncVarGameObjectEqual made protected again so that Weaver finds it again ([165a1dd](https://github.com/MirrorNG/MirrorNG/commit/165a1dd94cd1a7bebc3365c4f40f968f500043a5))
* NetworkBehaviour.SyncVarNetworkIdentityEqual made protected again so that Weaver finds it again ([20a2d09](https://github.com/MirrorNG/MirrorNG/commit/20a2d09d07ab2c47a204e5d583b538a92f72231e))

## [15.0.5](https://github.com/MirrorNG/MirrorNG/compare/15.0.4-master...15.0.5-master) (2020-03-08)


### Bug Fixes

* don't crash when stopping the client ([f584388](https://github.com/MirrorNG/MirrorNG/commit/f584388a16e746ac5c3000123a02a5c77387765e))
* race condition closing tcp connections ([717f1f5](https://github.com/MirrorNG/MirrorNG/commit/717f1f5ad783e13a6d55920e454cb91f380cd621))

## [15.0.4](https://github.com/MirrorNG/MirrorNG/compare/15.0.3-master...15.0.4-master) (2020-03-08)


### Bug Fixes

* attributes causing a NRE ([#69](https://github.com/MirrorNG/MirrorNG/issues/69)) ([fc99c67](https://github.com/MirrorNG/MirrorNG/commit/fc99c67712564e2d983674b37858591903294f1a))

## [15.0.3](https://github.com/MirrorNG/MirrorNG/compare/15.0.2-master...15.0.3-master) (2020-03-08)


### Bug Fixes

* NetworkIdentity.RebuildObservers: added missing null check for observers coming from components that implement OnRebuildObservers. Previously this caused a NullReferenceException. ([a5f495a](https://github.com/MirrorNG/MirrorNG/commit/a5f495a77485b972cf39f8a42bae6d7d852be38c))
* SendToObservers missing result variable ([9c09c26](https://github.com/MirrorNG/MirrorNG/commit/9c09c26a5cd28cadae4049fea71cddc38c00cf79))

## [15.0.2](https://github.com/MirrorNG/MirrorNG/compare/15.0.1-master...15.0.2-master) (2020-03-06)


### Bug Fixes

* rooms demo ([44598e5](https://github.com/MirrorNG/MirrorNG/commit/44598e58325c877bd6b53ee5a77dd95e421ec404))

## [15.0.1](https://github.com/MirrorNG/MirrorNG/compare/15.0.0-master...15.0.1-master) (2020-03-06)


### Bug Fixes

* chat example works ([0609d50](https://github.com/MirrorNG/MirrorNG/commit/0609d50d9b93afd3b42d97ddcd00d32e8aaa0db5))
* there is no lobby example ([b1e05ef](https://github.com/MirrorNG/MirrorNG/commit/b1e05efb19984ce615d20a16a6c4b09a8897da6a))

# [15.0.0](https://github.com/MirrorNG/MirrorNG/compare/14.0.1-master...15.0.0-master) (2020-03-05)


### Code Refactoring

* Remove networkAddress from NetworkManager ([#67](https://github.com/MirrorNG/MirrorNG/issues/67)) ([e89c32d](https://github.com/MirrorNG/MirrorNG/commit/e89c32dc16b3d9b9cdcb38f0d7d170da94dbf874))


### BREAKING CHANGES

* StartClient now receives the server ip
* NetworkManager no longer has NetworkAddress

## [14.0.1](https://github.com/MirrorNG/MirrorNG/compare/14.0.0-master...14.0.1-master) (2020-03-04)


### Bug Fixes

* Avoid FindObjectOfType when not needed ([#66](https://github.com/MirrorNG/MirrorNG/issues/66)) ([e2a4afd](https://github.com/MirrorNG/MirrorNG/commit/e2a4afd0b9ca8dea720acb9c558efca210bd8e71))

# [14.0.0](https://github.com/MirrorNG/MirrorNG/compare/13.0.0-master...14.0.0-master) (2020-03-03)


* Assign/Remove client authority now throws exception ([7679d3b](https://github.com/MirrorNG/MirrorNG/commit/7679d3bef369de5245fd301b33e85dbdd74e84cd))


### BREAKING CHANGES

* Assign/Remove client authority throws exception instead of returning boolean

# [13.0.0](https://github.com/MirrorNG/MirrorNG/compare/12.0.2-master...13.0.0-master) (2020-03-02)


* Removed LLAPI ([b0c936c](https://github.com/MirrorNG/MirrorNG/commit/b0c936cb7d1a803b7096806a905a4c121e45bcdf))


### BREAKING CHANGES

* Removed LLAPITransport

## [12.0.2](https://github.com/MirrorNG/MirrorNG/compare/12.0.1-master...12.0.2-master) (2020-02-29)


### Bug Fixes

* NetworkIdentity.OnStartLocalPlayer catches exceptions now too. fixes a potential bug where an exception in PlayerInventory.OnStartLocalPlayer would cause PlayerEquipment.OnStartLocalPlayer to not be called ([5ed5f84](https://github.com/MirrorNG/MirrorNG/commit/5ed5f844090442e16e78f466f7a785881283fbd4))

## [12.0.1](https://github.com/MirrorNG/MirrorNG/compare/12.0.0-master...12.0.1-master) (2020-02-29)


### Bug Fixes

* disconnect properly from the server ([c89bb51](https://github.com/MirrorNG/MirrorNG/commit/c89bb513e536f256e55862b723487bb21281572e))

# [12.0.0](https://github.com/MirrorNG/MirrorNG/compare/11.1.0-master...12.0.0-master) (2020-02-28)


* Simplify unpacking messages (#65) ([c369da8](https://github.com/MirrorNG/MirrorNG/commit/c369da84dc34dbbde68a7b30758a6a14bc2573b1)), closes [#65](https://github.com/MirrorNG/MirrorNG/issues/65)


### BREAKING CHANGES

* MessagePacker.UnpackMessage replaced by UnpackId

# [11.1.0](https://github.com/MirrorNG/MirrorNG/compare/11.0.0-master...11.1.0-master) (2020-02-27)


### Bug Fixes

* Add missing channelId to NetworkConnectionToClient.Send calls ([#1509](https://github.com/MirrorNG/MirrorNG/issues/1509)) ([b8bcd9a](https://github.com/MirrorNG/MirrorNG/commit/b8bcd9ad25895eee940a3daaf6fe7ed82eaf76ac))
* build in IL2CPP ([#1524](https://github.com/MirrorNG/MirrorNG/issues/1524)) ([59faa81](https://github.com/MirrorNG/MirrorNG/commit/59faa819262a166024b16d854e410c7e51763e6a)), closes [#1519](https://github.com/MirrorNG/MirrorNG/issues/1519) [#1520](https://github.com/MirrorNG/MirrorNG/issues/1520)
* Fixed NetworkRoomManager Template ([1662c5a](https://github.com/MirrorNG/MirrorNG/commit/1662c5a139363dbe61784bb90ae6544ec74478c3))
* Fixed toc link ([3a0c7fb](https://github.com/MirrorNG/MirrorNG/commit/3a0c7fb1ecd9d8715e797a7665ab9a6a7cb19e2a))
* Host Player Ready Race Condition ([#1498](https://github.com/MirrorNG/MirrorNG/issues/1498)) ([4c4a52b](https://github.com/MirrorNG/MirrorNG/commit/4c4a52bff95e7c56f065409b1399955813f3e145))
* NetworkIdentity.SetClientOwner: overwriting the owner was still possible even though it shouldn't be. all caller functions double check and return early if it already has an owner, so we should do the same here. ([548db52](https://github.com/MirrorNG/MirrorNG/commit/548db52fdf224f06ba9d8b2d75460d31181b7066))
* NetworkServer.SpawnObjects: return false if server isn't running ([d4d524d](https://github.com/MirrorNG/MirrorNG/commit/d4d524dad2a0a9d89538e6212dceda6bea71d2b7))
* properly detect NT rotation ([#1516](https://github.com/MirrorNG/MirrorNG/issues/1516)) ([f0a993c](https://github.com/MirrorNG/MirrorNG/commit/f0a993c1064384e0b3bd690d4d66be38875ed50e))
* return & continue on separate line ([#1504](https://github.com/MirrorNG/MirrorNG/issues/1504)) ([61fdd89](https://github.com/MirrorNG/MirrorNG/commit/61fdd892d9e6882e1e51094a2ceddfadc8fd1ebc))
* Room example to use new override ([e1d1d41](https://github.com/MirrorNG/MirrorNG/commit/e1d1d41ed69f192b5fb91f92dcdeae1ee057d38f))
* SendToAll sends to that exact connection if it is detected as local connection, instead of falling back to the .localConnection field which might be something completely different. ([4b90aaf](https://github.com/MirrorNG/MirrorNG/commit/4b90aafe12970e00949ee43b07b9edd5213745da))
* SendToObservers sends to that exact connection if it is detected as local connection, instead of falling back to the .localConnection field which might be something completely different. ([4267983](https://github.com/MirrorNG/MirrorNG/commit/426798313920d23548048aa1c678167fd9b45cbd))
* SendToReady sends to that exact connection if it is detected as local connection, instead of falling back to the .localConnection field which might be something completely different. ([4596b19](https://github.com/MirrorNG/MirrorNG/commit/4596b19dd959722d5dc659552206fe90c015fb01))


### Features

* Added NetworkConnection to OnRoomServerSceneLoadedForPlayer ([b5dfcf4](https://github.com/MirrorNG/MirrorNG/commit/b5dfcf45bc9838e89dc37b00cf3653688083bdd8))
* Check for client authority in CmdClientToServerSync ([#1500](https://github.com/MirrorNG/MirrorNG/issues/1500)) ([8b359ff](https://github.com/MirrorNG/MirrorNG/commit/8b359ff6d07352a751f200768dcde6febd8e9303))
* Check for client authority in NetworkAnimator Cmd's ([#1501](https://github.com/MirrorNG/MirrorNG/issues/1501)) ([ecc0659](https://github.com/MirrorNG/MirrorNG/commit/ecc0659b87f3d910dc2370f4861ec70244a25622))
* Cosmetic Enhancement of Network Manager ([#1512](https://github.com/MirrorNG/MirrorNG/issues/1512)) ([f53b12b](https://github.com/MirrorNG/MirrorNG/commit/f53b12b2f7523574d7ceffa2a833dbd7fac755c9))
* NetworkSceneChecker use Scene instead of string name ([#1496](https://github.com/MirrorNG/MirrorNG/issues/1496)) ([7bb80e3](https://github.com/MirrorNG/MirrorNG/commit/7bb80e3b796f2c69d0958519cf1b4a9f4373268b))

# [11.0.0](https://github.com/MirrorNG/MirrorNG/compare/10.0.0-master...11.0.0-master) (2020-02-13)


* Remove all compiler defines ([a394345](https://github.com/MirrorNG/MirrorNG/commit/a3943459598d30a325fb1e1315b84c0dedf1741c))


### Features

* Block Play Mode and Builds for Weaver Errors ([#1479](https://github.com/MirrorNG/MirrorNG/issues/1479)) ([0e80e19](https://github.com/MirrorNG/MirrorNG/commit/0e80e1996fb2673364169782c330e69cd598a21d))
* Disposable PooledNetworkReader / PooledNetworkWriter ([#1490](https://github.com/MirrorNG/MirrorNG/issues/1490)) ([bb55baa](https://github.com/MirrorNG/MirrorNG/commit/bb55baa679ae780e127ed5817ef10d7f12cd08c8))


### BREAKING CHANGES

* removed compilation defines,  use upm version defines instead

# [10.0.0](https://github.com/MirrorNG/MirrorNG/compare/9.1.0-master...10.0.0-master) (2020-02-12)


* Simplify AddPlayerForConnection (#62) ([fb26755](https://github.com/MirrorNG/MirrorNG/commit/fb267557af292e048df248d4f85fff3569ac2963)), closes [#62](https://github.com/MirrorNG/MirrorNG/issues/62)


### BREAKING CHANGES

* AddPlayerForConnection no longer receives the client

* fix compilatio errors

* fix build errors

# [9.1.0](https://github.com/MirrorNG/MirrorNG/compare/9.0.0-master...9.1.0-master) (2020-02-12)


### Bug Fixes

* weaver support array of custom types ([#1470](https://github.com/MirrorNG/MirrorNG/issues/1470)) ([d0b0bc9](https://github.com/MirrorNG/MirrorNG/commit/d0b0bc92bc33ff34491102a2f9e1855f2a5ed476))


### Features

* Added Read<T> Method to NetworkReader ([#1480](https://github.com/MirrorNG/MirrorNG/issues/1480)) ([58df3fd](https://github.com/MirrorNG/MirrorNG/commit/58df3fd6d6f53336668536081bc33e2ca22fd38d))
* supports scriptable objects ([#1471](https://github.com/MirrorNG/MirrorNG/issues/1471)) ([0f10c72](https://github.com/MirrorNG/MirrorNG/commit/0f10c72744864ac55d2e1aa96ba8d7713c77d9e7))

# [9.0.0](https://github.com/MirrorNG/MirrorNG/compare/8.0.1-master...9.0.0-master) (2020-02-08)


### Bug Fixes

* don't report error when stopping the server ([c965d4b](https://github.com/MirrorNG/MirrorNG/commit/c965d4b0ff32288ebe4e5c63a43e32559203deb1))


### Features

* awaitable connect ([#55](https://github.com/MirrorNG/MirrorNG/issues/55)) ([952e8a4](https://github.com/MirrorNG/MirrorNG/commit/952e8a43e2b2e4443e24865c60af1ee47467e4cf))


### BREAKING CHANGES

* ClientConnect replaced with ClientConnectAsync
that can be awaited

* fix: TCP transport for async compliance

* use async connect

* Ignore telepathy tests until they are fixed

* It is ok to interrupt sockets

* Remove some warnings

* Remove some warnings

* Remove some warnings

* Remove some warnings

* Remove some warnings

* Remove some warnings

* Remove some warnings

## [8.0.1](https://github.com/MirrorNG/MirrorNG/compare/8.0.0-master...8.0.1-master) (2020-02-06)


### Bug Fixes

* first connection id = 1 ([#60](https://github.com/MirrorNG/MirrorNG/issues/60)) ([891dab9](https://github.com/MirrorNG/MirrorNG/commit/891dab92d065821ca46b47ef2d3eb27124058d74))

# [8.0.0](https://github.com/MirrorNG/MirrorNG/compare/7.0.0-master...8.0.0-master) (2020-02-06)


### Bug Fixes

* call callback after update dictionary in host ([#1476](https://github.com/MirrorNG/MirrorNG/issues/1476)) ([1736bb0](https://github.com/MirrorNG/MirrorNG/commit/1736bb0c42c0d2ad341e31a645658722de3bfe07))
* port network discovery ([d6a1154](https://github.com/MirrorNG/MirrorNG/commit/d6a1154e98c52e7873411ce9d7b87f7b294dc436))
* remove scriptableobject error Tests ([479b78b](https://github.com/MirrorNG/MirrorNG/commit/479b78bf3cabe93938bf61b7f8fd63ba46da0f4a))
* Telepathy reverted to older version to fix freezes when calling Client.Disconnect on some platforms like Windows 10 ([d0d77b6](https://github.com/MirrorNG/MirrorNG/commit/d0d77b661cd07e25887f0e2f4c2d72b4f65240a2))
* Telepathy updated to latest version. Threads are closed properly now. ([4007423](https://github.com/MirrorNG/MirrorNG/commit/4007423db28f7044f6aa97b108a6bfbe3f2d46a9))


* Renamed localEulerAnglesSensitivity (#1474) ([eee9692](https://github.com/MirrorNG/MirrorNG/commit/eee969201d69df1e1ee1f1623b55a78f6003fbb1)), closes [#1474](https://github.com/MirrorNG/MirrorNG/issues/1474)


### breaking

* Transports can now provide their Uri ([#1454](https://github.com/MirrorNG/MirrorNG/issues/1454)) ([b916064](https://github.com/MirrorNG/MirrorNG/commit/b916064856cf78f1c257f0de0ffe8c9c1ab28ce7)), closes [#38](https://github.com/MirrorNG/MirrorNG/issues/38)


### Features

* Implemented NetworkReaderPool ([#1464](https://github.com/MirrorNG/MirrorNG/issues/1464)) ([9257112](https://github.com/MirrorNG/MirrorNG/commit/9257112c65c92324ad0bd51e4a017aa1b4c9c1fc))
* LAN Network discovery ([#1453](https://github.com/MirrorNG/MirrorNG/issues/1453)) ([e75b45f](https://github.com/MirrorNG/MirrorNG/commit/e75b45f8889478456573ea395694b4efc560ace0)), closes [#38](https://github.com/MirrorNG/MirrorNG/issues/38)
* Mirror Icon for all components ([#1452](https://github.com/MirrorNG/MirrorNG/issues/1452)) ([a7efb13](https://github.com/MirrorNG/MirrorNG/commit/a7efb13e29e0bc9ed695a86070e3eb57b7506b4c))
* supports scriptable objects ([4b8f819](https://github.com/MirrorNG/MirrorNG/commit/4b8f8192123fe0b79ea71f2255a4bbac404c88b1))


### BREAKING CHANGES

* localEulerAnglesSensitivity renamed to localRotationSensitivity
* Make the server uri method mandatory in transports

Co-authored-by: MrGadget <chris@clevertech.net>

# [7.0.0](https://github.com/MirrorNG/MirrorNG/compare/6.0.0-master...7.0.0-master) (2020-01-27)


### Features

* Network Scene Checker Component ([#1271](https://github.com/MirrorNG/MirrorNG/issues/1271)) ([71c0d3b](https://github.com/MirrorNG/MirrorNG/commit/71c0d3b2ee1bbdb29d1c39ee6eca3ef9635d70bf))
* network writer and reader now support uri ([0c2556a](https://github.com/MirrorNG/MirrorNG/commit/0c2556ac64bd93b9e52dae34cf8d84db114b4107))


* Rename NetworkServer.localClientActive ([7cd0894](https://github.com/MirrorNG/MirrorNG/commit/7cd0894853b97fb804ae15b8a75b75c9d7bc04a7))
* Simplify spawning ([c87a38a](https://github.com/MirrorNG/MirrorNG/commit/c87a38a4ff0c350901138b90db7fa8e61b1ab7db))


### BREAKING CHANGES

* rename localClientActive to LocalClientActive
* Spawn no longer receives NetworkClient

# [6.0.0](https://github.com/MirrorNG/MirrorNG/compare/5.0.0-master...6.0.0-master) (2020-01-22)


### Bug Fixes

* compilation error ([df7baa4](https://github.com/MirrorNG/MirrorNG/commit/df7baa4db0d347ee69c17bad9f9e56ccefb54fab))
* compilation error ([dc74256](https://github.com/MirrorNG/MirrorNG/commit/dc74256fc380974ad6df59b5d1dee3884b879bd7))
* Fix Room Slots for clients ([#1439](https://github.com/MirrorNG/MirrorNG/issues/1439)) ([268753c](https://github.com/MirrorNG/MirrorNG/commit/268753c3bd0a9c0695d8d4510a129685be364a11))

# [5.0.0](https://github.com/MirrorNG/MirrorNG/compare/4.0.0-master...5.0.0-master) (2020-01-19)

# [4.0.0](https://github.com/MirrorNG/MirrorNG/compare/3.1.0-master...4.0.0-master) (2020-01-18)

# [3.1.0](https://github.com/MirrorNG/MirrorNG/compare/3.0.4-master...3.1.0-master) (2020-01-16)


### Bug Fixes

* Decouple ChatWindow from player ([#1429](https://github.com/MirrorNG/MirrorNG/issues/1429)) ([42a2f9b](https://github.com/MirrorNG/MirrorNG/commit/42a2f9b853667ef9485a1d4a31979fcf1153c0d7))
* StopHost with offline scene calls scene change twice ([#1409](https://github.com/MirrorNG/MirrorNG/issues/1409)) ([a0c96f8](https://github.com/MirrorNG/MirrorNG/commit/a0c96f85189bfc9b5a936a8a33ebda34b460f17f))
* Telepathy works on .net core again ([cb3d9f0](https://github.com/MirrorNG/MirrorNG/commit/cb3d9f0d08a961b345ce533d1ce64602f7041e1c))


### Features

* Add Sensitivity to NetworkTransform ([#1425](https://github.com/MirrorNG/MirrorNG/issues/1425)) ([f69f174](https://github.com/MirrorNG/MirrorNG/commit/f69f1743c54aa7810c5a218e2059c115760c54a3))

## [3.0.4](https://github.com/MirrorNG/MirrorNG/compare/3.0.3-master...3.0.4-master) (2020-01-12)


### Bug Fixes

* comply with MIT license in upm package ([b879bef](https://github.com/MirrorNG/MirrorNG/commit/b879bef4295e48c19d96a1d45536a11ea47380f3))

## [3.0.3](https://github.com/MirrorNG/MirrorNG/compare/3.0.2-master...3.0.3-master) (2020-01-12)


### Bug Fixes

* auto reference mirrorng assembly ([93f8688](https://github.com/MirrorNG/MirrorNG/commit/93f8688b39822bb30ed595ca36f44a8a556bec85))
* MirrorNG works with 2019.2 ([9f35d6b](https://github.com/MirrorNG/MirrorNG/commit/9f35d6be427843aa7dd140cde32dd843c62147ce))

## [3.0.2](https://github.com/MirrorNG/MirrorNG/compare/3.0.1-master...3.0.2-master) (2020-01-12)


### Bug Fixes

* remove Tests from upm package ([#34](https://github.com/MirrorNG/MirrorNG/issues/34)) ([8d8ea0f](https://github.com/MirrorNG/MirrorNG/commit/8d8ea0f10743044e4a9a3d6c5b9f9973cf48e28b))

## [3.0.1](https://github.com/MirrorNG/MirrorNG/compare/3.0.0-master...3.0.1-master) (2020-01-11)


### Bug Fixes

* remove Tests from UPM ([#33](https://github.com/MirrorNG/MirrorNG/issues/33)) ([8f42af0](https://github.com/MirrorNG/MirrorNG/commit/8f42af0a3992cfa549eb404ad9f9693101895ce9))

# [3.0.0](https://github.com/MirrorNG/MirrorNG/compare/2.0.0-master...3.0.0-master) (2020-01-11)


### Bug Fixes

* [#723](https://github.com/MirrorNG/MirrorNG/issues/723) - NetworkTransform teleport works properly now ([fd7dc5e](https://github.com/MirrorNG/MirrorNG/commit/fd7dc5e226a76b27250fb503a98f23eb579387f8))
* fix release pipeline ([2a3db0b](https://github.com/MirrorNG/MirrorNG/commit/2a3db0b398cd641c3e1ba65a32b34822e9c9169f))
* release job requires node 10 ([3f50e63](https://github.com/MirrorNG/MirrorNG/commit/3f50e63bc32f4942e1c130c681dabd34ae81b117))
* remove tests from npm package ([#32](https://github.com/MirrorNG/MirrorNG/issues/32)) ([5ed9b4f](https://github.com/MirrorNG/MirrorNG/commit/5ed9b4f1235d5d1dc54c3f50bb1aeefd5dbe3038))
* syntax error in release job ([2eeaea4](https://github.com/MirrorNG/MirrorNG/commit/2eeaea41bc81cfe0c191b39da912adc565e11ec7))


### Features

* Network Animator can reset triggers ([#1420](https://github.com/MirrorNG/MirrorNG/issues/1420)) ([dffdf02](https://github.com/MirrorNG/MirrorNG/commit/dffdf02be596db3d35bdd8d19ba6ada7d796a137))
* NetworkAnimator warns if you use it incorrectly ([#1424](https://github.com/MirrorNG/MirrorNG/issues/1424)) ([c30e4a9](https://github.com/MirrorNG/MirrorNG/commit/c30e4a9f83921416f936ef5fd1bb0e2b3a410807))


### Performance Improvements

* Use NetworkWriterPool in NetworkAnimator ([#1421](https://github.com/MirrorNG/MirrorNG/issues/1421)) ([7d472f2](https://github.com/MirrorNG/MirrorNG/commit/7d472f21f9a807357df244a3f0ac259dd431661f))
* Use NetworkWriterPool in NetworkTransform ([#1422](https://github.com/MirrorNG/MirrorNG/issues/1422)) ([a457845](https://github.com/MirrorNG/MirrorNG/commit/a4578458a15e3d2840a49dd029b4c404cadf85a4))

# [2.0.0](https://github.com/MirrorNG/MirrorNG/compare/1.1.2-master...2.0.0-master) (2020-01-09)

## [1.1.2](https://github.com/MirrorNG/MirrorNG/compare/1.1.1-master...1.1.2-master) (2020-01-09)


### Bug Fixes

* [#1241](https://github.com/MirrorNG/MirrorNG/issues/1241) - Telepathy updated to latest version. All tests are passing again. Thread.Interrupt was replaced by Abort+Join. ([228b32e](https://github.com/MirrorNG/MirrorNG/commit/228b32e1da8e407e1d63044beca0fd179f0835b4))
* [#1278](https://github.com/MirrorNG/MirrorNG/issues/1278) - only call initial state SyncVar hooks on clients if the SyncVar value is different from the default one. ([#1414](https://github.com/MirrorNG/MirrorNG/issues/1414)) ([a3ffd12](https://github.com/MirrorNG/MirrorNG/commit/a3ffd1264c2ed2780e6e86ce83077fa756c01154))
* [#1380](https://github.com/MirrorNG/MirrorNG/issues/1380) - NetworkConnection.clientOwnedObjects changed from uint HashSet to NetworkIdentity HashSet for ease of use and to fix a bug where DestroyOwnedObjects wouldn't find a netId anymore in some cases. ([a71ecdb](https://github.com/MirrorNG/MirrorNG/commit/a71ecdba4a020f9f4648b8275ec9d17b19aff55f))
* FinishLoadSceneHost calls FinishStart host which now calls StartHostClient AFTER server online scene was loaded. Previously there was a race condition where StartHostClient was called immediately in StartHost, before the scene change even finished. This was still from UNET. ([df9c29a](https://github.com/MirrorNG/MirrorNG/commit/df9c29a6b3f9d0c8adbaff5a500e54abddb303b3))

## [1.1.1](https://github.com/MirrorNG/MirrorNG/compare/1.1.0-master...1.1.1-master) (2020-01-05)


### Bug Fixes

* add Changelog metadata fix [#31](https://github.com/MirrorNG/MirrorNG/issues/31) ([c67de22](https://github.com/MirrorNG/MirrorNG/commit/c67de2216aa331de10bba2e09ea3f77e6b1caa3c))

# [1.1.0](https://github.com/MirrorNG/MirrorNG/compare/1.0.0-master...1.1.0-master) (2020-01-04)


### Features

* include generated changelog ([#27](https://github.com/MirrorNG/MirrorNG/issues/27)) ([a60f1ac](https://github.com/MirrorNG/MirrorNG/commit/a60f1acd3a544639a5e58a8946e75fd6c9012327))
