# [41.1.0](https://github.com/MirrorNG/MirrorNG/compare/41.0.0-master...41.1.0-master) (2020-07-15)


### Features

* Transports can have multiple uri ([#292](https://github.com/MirrorNG/MirrorNG/issues/292)) ([155a29c](https://github.com/MirrorNG/MirrorNG/commit/155a29c053421f870241a75427db748fbef08910))

# [41.0.0](https://github.com/MirrorNG/MirrorNG/compare/40.3.0-master...41.0.0-master) (2020-07-15)


### breaking

* AsyncFallbackTransport -> FallbackTransport ([f8f643a](https://github.com/MirrorNG/MirrorNG/commit/f8f643a6245777279de31dc8997a7ea84328533e))
* AsyncMultiplexTransport -> MultiplexTransport ([832b7f9](https://github.com/MirrorNG/MirrorNG/commit/832b7f9528595e45769790c4be4fd94e873c96f4))
* rename AsyncWsTransport -> WsTransport ([9c394bc](https://github.com/MirrorNG/MirrorNG/commit/9c394bc96192a50ad273371b66c9289d75402dc6))


### Features

* Transports may support any number of schemes ([#291](https://github.com/MirrorNG/MirrorNG/issues/291)) ([2af7b9d](https://github.com/MirrorNG/MirrorNG/commit/2af7b9d19cef3878147eee412adf2b9b32c91147))


### BREAKING CHANGES

* rename AsyncMultiplexTransport -> MultiplexTransport
* rename AsyncFallbackTransport -> FallbackTransport
* rename AsyncWsTransport -> WsTransport

# [40.3.0](https://github.com/MirrorNG/MirrorNG/compare/40.2.0-master...40.3.0-master) (2020-07-14)


### Bug Fixes

* SceneManager Exceptions and Tests ([#287](https://github.com/MirrorNG/MirrorNG/issues/287)) ([388d218](https://github.com/MirrorNG/MirrorNG/commit/388d21872bb8b4c7f9d3742ecfa9b857ee734dfa))


### Features

* Server and Client share the same scene loading method ([#286](https://github.com/MirrorNG/MirrorNG/issues/286)) ([acb6dd1](https://github.com/MirrorNG/MirrorNG/commit/acb6dd192244adcfab15d013a96c7402151d226b))

# [40.2.0](https://github.com/MirrorNG/MirrorNG/compare/40.1.1-master...40.2.0-master) (2020-07-14)


### Features

* additive scene msging added to server ([#285](https://github.com/MirrorNG/MirrorNG/issues/285)) ([bd7a17a](https://github.com/MirrorNG/MirrorNG/commit/bd7a17a65fbc9aed64aaef6c65641697e8d89a74))

## [40.1.1](https://github.com/MirrorNG/MirrorNG/compare/40.1.0-master...40.1.1-master) (2020-07-14)


### Bug Fixes

* prevent NRE when operating as a separated client and server ([#283](https://github.com/MirrorNG/MirrorNG/issues/283)) ([e10e198](https://github.com/MirrorNG/MirrorNG/commit/e10e198b4865fc8c941244c3e368eebc6cf73179))

# [40.1.0](https://github.com/MirrorNG/MirrorNG/compare/40.0.0-master...40.1.0-master) (2020-07-14)


### Features

* Transports can tell if they are supported ([#282](https://github.com/MirrorNG/MirrorNG/issues/282)) ([890c6b8](https://github.com/MirrorNG/MirrorNG/commit/890c6b8808ccbf4f4ffffae8c00a9d897ccac7e4))

# [40.0.0](https://github.com/MirrorNG/MirrorNG/compare/39.0.0-master...40.0.0-master) (2020-07-14)


### Features

* LocalPlayer attribute now throws error ([#277](https://github.com/MirrorNG/MirrorNG/issues/277)) ([15aa537](https://github.com/MirrorNG/MirrorNG/commit/15aa537947cd14e4d71853f1786c387519d8828b))


### BREAKING CHANGES

* [LocalPlayerCallback] is now [LocalPlayer(error = false)]

* Local Player guard

Co-authored-by: Paul Pacheco <paul.pacheco@aa.com>

# [39.0.0](https://github.com/MirrorNG/MirrorNG/compare/38.0.0-master...39.0.0-master) (2020-07-14)


### Features

* Client attribute now throws error ([#274](https://github.com/MirrorNG/MirrorNG/issues/274)) ([f1b52f3](https://github.com/MirrorNG/MirrorNG/commit/f1b52f3d23e9aa50b5fab8509f3c769e97eac2e7))


### BREAKING CHANGES

* [ClientCallback] is now [Client(error = false)]

# [38.0.0](https://github.com/MirrorNG/MirrorNG/compare/37.0.1-master...38.0.0-master) (2020-07-14)


### Features

* HasAuthority attribute now throws error ([#276](https://github.com/MirrorNG/MirrorNG/issues/276)) ([da2355b](https://github.com/MirrorNG/MirrorNG/commit/da2355b4c1a51dbcbf6ceb405b6fc7b5bb14fa14))


### BREAKING CHANGES

* [HasAuthorityCallback] is now [HasAuthority(error = false)]

* fix test

Co-authored-by: Paul Pacheco <paul.pacheco@aa.com>

## [37.0.1](https://github.com/MirrorNG/MirrorNG/compare/37.0.0-master...37.0.1-master) (2020-07-14)


### Bug Fixes

* smell cleanup left if bug. repaired with parenthesis. ([#275](https://github.com/MirrorNG/MirrorNG/issues/275)) ([dd52be3](https://github.com/MirrorNG/MirrorNG/commit/dd52be3bb9406de7b2527c72fce90c9ed6c9d5bf))

# [37.0.0](https://github.com/MirrorNG/MirrorNG/compare/36.0.0-master...37.0.0-master) (2020-07-13)


### Features

* Server attribute now throws error ([#270](https://github.com/MirrorNG/MirrorNG/issues/270)) ([f3b5dc4](https://github.com/MirrorNG/MirrorNG/commit/f3b5dc4fef5fba05e585d274d9df05c3954ff6c7))


### BREAKING CHANGES

* [ServerCallback] is now [Server(error = false)]

* fixed weaver test

* Remove unused code

* fix comment

* document replacement of ServerCallback

* No need to be serializable

* Exception should be serializable?

* Fix code smell

* No need to implement interface,  parent does

Co-authored-by: Paul Pacheco <paul.pacheco@aa.com>

# [36.0.0](https://github.com/MirrorNG/MirrorNG/compare/35.3.4-master...36.0.0-master) (2020-07-13)


### breaking

* Rename [Command] to [ServerRpc] ([#271](https://github.com/MirrorNG/MirrorNG/issues/271)) ([fff7459](https://github.com/MirrorNG/MirrorNG/commit/fff7459801fc637c641757c516f85b4d685e0ad1))


### BREAKING CHANGES

* Renamed [Command] to [ServerRpc]

## [35.3.4](https://github.com/MirrorNG/MirrorNG/compare/35.3.3-master...35.3.4-master) (2020-07-13)


### Bug Fixes

* add tests for NetworkTransform and NetworkRigidbody ([#273](https://github.com/MirrorNG/MirrorNG/issues/273)) ([e9621dd](https://github.com/MirrorNG/MirrorNG/commit/e9621ddebd50637680fad8fe743c7c99afea3f84))
* NinjaWS code smells ([#272](https://github.com/MirrorNG/MirrorNG/issues/272)) ([71d9428](https://github.com/MirrorNG/MirrorNG/commit/71d942804c0d404f287dc51b7bcdd1fcc39bcee8))

## [35.3.3](https://github.com/MirrorNG/MirrorNG/compare/35.3.2-master...35.3.3-master) (2020-07-13)


### Bug Fixes

* Misc code smells ([#269](https://github.com/MirrorNG/MirrorNG/issues/269)) ([23dcca6](https://github.com/MirrorNG/MirrorNG/commit/23dcca61ff7c41e8b9f61579605fd56ee82c70e0))

## [35.3.2](https://github.com/MirrorNG/MirrorNG/compare/35.3.1-master...35.3.2-master) (2020-07-13)


### Bug Fixes

* remove customHandling as its no longer used ([#265](https://github.com/MirrorNG/MirrorNG/issues/265)) ([dbd9d84](https://github.com/MirrorNG/MirrorNG/commit/dbd9d84ee46ac90a8d78daba0c23fc9be71ca77d))

## [35.3.1](https://github.com/MirrorNG/MirrorNG/compare/35.3.0-master...35.3.1-master) (2020-07-13)


### Bug Fixes

* AdditiveSceneExample missing comp and assignments ([#267](https://github.com/MirrorNG/MirrorNG/issues/267)) ([ab394b8](https://github.com/MirrorNG/MirrorNG/commit/ab394b8f7e823b8c3882de35eaa54c05fbd9316e))
* NRE on gamemanager in scene ([#268](https://github.com/MirrorNG/MirrorNG/issues/268)) ([58a124a](https://github.com/MirrorNG/MirrorNG/commit/58a124a99c267091142f00adc7f8898160a9dd97))

# [35.3.0](https://github.com/MirrorNG/MirrorNG/compare/35.2.0-master...35.3.0-master) (2020-07-13)


### Bug Fixes

* Message base class not being Serialized if processed in the wrong order ([#2023](https://github.com/MirrorNG/MirrorNG/issues/2023)) ([3418fa2](https://github.com/MirrorNG/MirrorNG/commit/3418fa210602cf1a9b9331b198ac47d8a3cabe69))
* not removing server if id is empty ([#2078](https://github.com/MirrorNG/MirrorNG/issues/2078)) ([f717945](https://github.com/MirrorNG/MirrorNG/commit/f7179455256bb7341ffa9e6921fe0de50498150a))


### Features

* ClientRpc no longer need Rpc prefix ([#2086](https://github.com/MirrorNG/MirrorNG/issues/2086)) ([eb93c34](https://github.com/MirrorNG/MirrorNG/commit/eb93c34b330189c79727b0332bb66f3675cfd641))
* Commands no longer need Cmd prefix ([#2084](https://github.com/MirrorNG/MirrorNG/issues/2084)) ([b6d1d09](https://github.com/MirrorNG/MirrorNG/commit/b6d1d09f846f7cf0310db0db9d931a9cfbbb36b2))
* Sync Events no longer need Event prefix ([#2087](https://github.com/MirrorNG/MirrorNG/issues/2087)) ([ed40c2d](https://github.com/MirrorNG/MirrorNG/commit/ed40c2d210f174f1ed50b1e929e4fb161414f228))
* TargetRpc no longer need Target prefix ([#2085](https://github.com/MirrorNG/MirrorNG/issues/2085)) ([d89ac9f](https://github.com/MirrorNG/MirrorNG/commit/d89ac9fb052c17c2edfdf381aff35f70d23f4f0a))


### Performance Improvements

* Use invokeRepeating instead of Update ([#2066](https://github.com/MirrorNG/MirrorNG/issues/2066)) ([264f9b8](https://github.com/MirrorNG/MirrorNG/commit/264f9b8f945f0294a8420202abcd0c80e27e6ee6))

# [35.2.0](https://github.com/MirrorNG/MirrorNG/compare/35.1.0-master...35.2.0-master) (2020-07-12)


### Bug Fixes

* add client only test for FinishLoadScene ([#262](https://github.com/MirrorNG/MirrorNG/issues/262)) ([50e7fa6](https://github.com/MirrorNG/MirrorNG/commit/50e7fa6e287fee09afbe36a51575f41c4bd50736))


### Features

* Commands no longer need to start with Cmd ([#263](https://github.com/MirrorNG/MirrorNG/issues/263)) ([9578e19](https://github.com/MirrorNG/MirrorNG/commit/9578e19ff70bf3a09a9fe31926c8ac337f945ba9))
* throw exception if assigning incorrect asset id ([#250](https://github.com/MirrorNG/MirrorNG/issues/250)) ([7741fb1](https://github.com/MirrorNG/MirrorNG/commit/7741fb1f11abc8eb2aec8c1a94ac53380ac5a562))

# [35.1.0](https://github.com/MirrorNG/MirrorNG/compare/35.0.3-master...35.1.0-master) (2020-07-12)


### Features

* Add Network Menu  ([#253](https://github.com/MirrorNG/MirrorNG/issues/253)) ([d81f444](https://github.com/MirrorNG/MirrorNG/commit/d81f444c42475439d24bf5b4abd2bbf15fd34e92))

## [35.0.3](https://github.com/MirrorNG/MirrorNG/compare/35.0.2-master...35.0.3-master) (2020-07-11)


### Bug Fixes

* code smell rename Ready ([#256](https://github.com/MirrorNG/MirrorNG/issues/256)) ([6d92d14](https://github.com/MirrorNG/MirrorNG/commit/6d92d1482cdd31fa663f7475f103476c65b7d875))
* Misc Code Smells ([#257](https://github.com/MirrorNG/MirrorNG/issues/257)) ([278a127](https://github.com/MirrorNG/MirrorNG/commit/278a1279dabefe04b0829015841de68b41e60a7b))

## [35.0.2](https://github.com/MirrorNG/MirrorNG/compare/35.0.1-master...35.0.2-master) (2020-07-11)


### Bug Fixes

* cleanup the server even after error ([#255](https://github.com/MirrorNG/MirrorNG/issues/255)) ([7bd015e](https://github.com/MirrorNG/MirrorNG/commit/7bd015eac1b77f0ad5974abb5c4c87a5d3da7b6d))

## [35.0.1](https://github.com/MirrorNG/MirrorNG/compare/35.0.0-master...35.0.1-master) (2020-07-11)


### Bug Fixes

* fix adding and saving Components ([2de7ecd](https://github.com/MirrorNG/MirrorNG/commit/2de7ecd93029bf5fd2fbb04ad4e47936cbb802cc))

# [35.0.0](https://github.com/MirrorNG/MirrorNG/compare/34.13.0-master...35.0.0-master) (2020-07-10)


### Features

* Component based NetworkSceneManager ([#244](https://github.com/MirrorNG/MirrorNG/issues/244)) ([7579d71](https://github.com/MirrorNG/MirrorNG/commit/7579d712ad97db98cd729c51568631e4c3257b58))


### BREAKING CHANGES

* NetworkManager no longer handles scene changes

# [34.13.0](https://github.com/MirrorNG/MirrorNG/compare/34.12.0-master...34.13.0-master) (2020-07-05)


### Features

* Spawn objects in clients in same order as server ([#247](https://github.com/MirrorNG/MirrorNG/issues/247)) ([b786646](https://github.com/MirrorNG/MirrorNG/commit/b786646f1859bb0e1836460c6319a507e1cc31aa))

# [34.12.0](https://github.com/MirrorNG/MirrorNG/compare/34.11.0-master...34.12.0-master) (2020-07-04)


### Features

* Example with 10k monster that change unfrequently ([2b2e71c](https://github.com/MirrorNG/MirrorNG/commit/2b2e71cc007dba2c1d90b565c4983814c1e0b7d1))

# [34.11.0](https://github.com/MirrorNG/MirrorNG/compare/34.10.1-master...34.11.0-master) (2020-07-04)


### Bug Fixes

* addingNetwork rigidbody icon and AddComponentMenu attribute ([#2051](https://github.com/MirrorNG/MirrorNG/issues/2051)) ([ab1b92f](https://github.com/MirrorNG/MirrorNG/commit/ab1b92f74b56787feb7c6fde87c0b9838b8d9d0f))
* calling base method when the first base class did not have the virtual method ([#2014](https://github.com/MirrorNG/MirrorNG/issues/2014)) ([4af72c3](https://github.com/MirrorNG/MirrorNG/commit/4af72c3a63e72dac6b3bab193dc58bfa3c44a975))
* changing namespace to match folder name ([#2037](https://github.com/MirrorNG/MirrorNG/issues/2037)) ([e36449c](https://github.com/MirrorNG/MirrorNG/commit/e36449cb22d8a2dede0133cf229bc12885c36bdb))
* Clean up roomSlots on clients in NetworkRoomPlayer ([5032ceb](https://github.com/MirrorNG/MirrorNG/commit/5032ceb00035679e0b80f59e91131cdfa8e0b1bb))
* Fallback and Multiplex now disable their transports when they are disabled  ([#2048](https://github.com/MirrorNG/MirrorNG/issues/2048)) ([61d44b2](https://github.com/MirrorNG/MirrorNG/commit/61d44b2d80c9616f784e855131ba6d1ee8a30136))
* If socket is undefined it will return false. See [#1486](https://github.com/MirrorNG/MirrorNG/issues/1486) ([#2017](https://github.com/MirrorNG/MirrorNG/issues/2017)) ([4ffff19](https://github.com/MirrorNG/MirrorNG/commit/4ffff192a69108b993cf963cfdece47b14ffdbf2))
* Network rigidbody fixes ([#2050](https://github.com/MirrorNG/MirrorNG/issues/2050)) ([0c30d33](https://github.com/MirrorNG/MirrorNG/commit/0c30d3398aaabcbf094a88a9c9c77ab4d5062acf))
* sync events can not have the same name if they are in different classes ([#2054](https://github.com/MirrorNG/MirrorNG/issues/2054)) ([c91308f](https://github.com/MirrorNG/MirrorNG/commit/c91308fb0461e54292940ce6fa42bb6cd9800d89))
* weaver now processes multiple SyncEvent per class ([#2055](https://github.com/MirrorNG/MirrorNG/issues/2055)) ([b316b35](https://github.com/MirrorNG/MirrorNG/commit/b316b35d46868a7e11c7b2005570efeec843efe1))


### Features

* adding demo for mirror cloud services ([#2026](https://github.com/MirrorNG/MirrorNG/issues/2026)) ([f1fdc95](https://github.com/MirrorNG/MirrorNG/commit/f1fdc959dcd62e7228ecfd656bc87cbabca8c1bc))
* adding log handler that sets console color ([#2001](https://github.com/MirrorNG/MirrorNG/issues/2001)) ([4623978](https://github.com/MirrorNG/MirrorNG/commit/46239783f313159ac47e192499aa8e7fcc5df0ec))
* Experimental NetworkRigidbody  ([#1822](https://github.com/MirrorNG/MirrorNG/issues/1822)) ([25285b1](https://github.com/MirrorNG/MirrorNG/commit/25285b1574c4e025373e86735ec3eb9734272fd2))
* More examples for Mirror Cloud Service ([#2029](https://github.com/MirrorNG/MirrorNG/issues/2029)) ([7d0e907](https://github.com/MirrorNG/MirrorNG/commit/7d0e907b73530c9a625eaf663837b7eeb36fcee6))

## [34.10.1](https://github.com/MirrorNG/MirrorNG/compare/34.10.0-master...34.10.1-master) (2020-07-04)


### Bug Fixes

* assign spawn locations and fix null refs in example ([#242](https://github.com/MirrorNG/MirrorNG/issues/242)) ([3adf343](https://github.com/MirrorNG/MirrorNG/commit/3adf3438578ff304f1216022aae8e043c52cd71d))
* folders for meta files no longer in the codebase ([#237](https://github.com/MirrorNG/MirrorNG/issues/237)) ([192fd16](https://github.com/MirrorNG/MirrorNG/commit/192fd1645986c515a804a01e0707c78241882676))
* remove pause network comment and log ([#238](https://github.com/MirrorNG/MirrorNG/issues/238)) ([1a8c09d](https://github.com/MirrorNG/MirrorNG/commit/1a8c09d8a5a8cf70508d4e42e4912e3989478ff1))

# [34.10.0](https://github.com/MirrorNG/MirrorNG/compare/34.9.4-master...34.10.0-master) (2020-07-04)


### Bug Fixes

* [#1659](https://github.com/MirrorNG/MirrorNG/issues/1659) Telepathy LateUpdate processes a limited amount of messages per tick to avoid deadlocks ([#1830](https://github.com/MirrorNG/MirrorNG/issues/1830)) ([d3dccd7](https://github.com/MirrorNG/MirrorNG/commit/d3dccd7a25e4b9171ac04e43a05954b56caefd4b))
* Added ClientOnly check ([fb927f8](https://github.com/MirrorNG/MirrorNG/commit/fb927f814110327867821dac8b0d69ca4251d4f6))
* Adding warning when adding handler with RegisterSpawnHandler if assetid already exists ([#1819](https://github.com/MirrorNG/MirrorNG/issues/1819)) ([7f26329](https://github.com/MirrorNG/MirrorNG/commit/7f26329e2db9d00da04bed40399af053436218bd))
* Adding warning when adding prefab with RegisterPrefab if assetid already exists ([#1828](https://github.com/MirrorNG/MirrorNG/issues/1828)) ([9f59e0c](https://github.com/MirrorNG/MirrorNG/commit/9f59e0c439707d66409a617b8f209187856eaf5f))
* Allowing overrides for virtual commands to call base method ([#1944](https://github.com/MirrorNG/MirrorNG/issues/1944)) ([b92da91](https://github.com/MirrorNG/MirrorNG/commit/b92da91d7a04f41098615ff2e2a35cf7ff479201))
* better error for Command, ClientRpc and TargetRpc marked as abstract ([#1947](https://github.com/MirrorNG/MirrorNG/issues/1947)) ([62257d8](https://github.com/MirrorNG/MirrorNG/commit/62257d8c4fc307ba3e23fbd01dcc950515c31e79))
* Better errors when trying to replace existing assetid ([#1827](https://github.com/MirrorNG/MirrorNG/issues/1827)) ([822b041](https://github.com/MirrorNG/MirrorNG/commit/822b04155def9859b24900c6e55a4253f85ebb3f))
* Cleaning up network objects when server stops ([#1864](https://github.com/MirrorNG/MirrorNG/issues/1864)) ([4c25122](https://github.com/MirrorNG/MirrorNG/commit/4c25122958978557173ec37ca400c47b2d8e834f))
* clear all message handlers on Shutdown ([#1829](https://github.com/MirrorNG/MirrorNG/issues/1829)) ([a6ab352](https://github.com/MirrorNG/MirrorNG/commit/a6ab3527acb9af8f6a68f0151e5231e4ee1a98e9))
* Don't call RegisterClientMessages every scene change ([#1865](https://github.com/MirrorNG/MirrorNG/issues/1865)) ([05c119f](https://github.com/MirrorNG/MirrorNG/commit/05c119f505390094c8f33e11568d40117360c49e))
* Don't call RegisterClientMessages twice ([#1842](https://github.com/MirrorNG/MirrorNG/issues/1842)) ([2a08aac](https://github.com/MirrorNG/MirrorNG/commit/2a08aac7cb8887934eb7eb8c232ce07976defe35))
* Fixed Capitalization ([c45deb8](https://github.com/MirrorNG/MirrorNG/commit/c45deb808e8e01a7b697e703be783aea2799d4d1))
* Fixing ClientScene UnregisterPrefab ([#1815](https://github.com/MirrorNG/MirrorNG/issues/1815)) ([9270765](https://github.com/MirrorNG/MirrorNG/commit/9270765bebf45c34a466694473b43c6d802b99d9))
* Improved error checking for ClientScene.RegisterPrefab ([#1823](https://github.com/MirrorNG/MirrorNG/issues/1823)) ([a0aa4f9](https://github.com/MirrorNG/MirrorNG/commit/a0aa4f9c1425d4eca3fe08cd2d87361f092ded6f))
* Improved error checking for ClientScene.RegisterPrefab with handler ([#1841](https://github.com/MirrorNG/MirrorNG/issues/1841)) ([54071da](https://github.com/MirrorNG/MirrorNG/commit/54071da3afb18d6289de5d0e41dc248e21088641))
* making weaver include public fields in base classes in auto generated Read/Write ([#1977](https://github.com/MirrorNG/MirrorNG/issues/1977)) ([3db57e5](https://github.com/MirrorNG/MirrorNG/commit/3db57e5f61ac0748d3a6296d8ea44c202830796f))
* NetworkRoomManager.minPlayers is now protected so it's available for derived classes. ([3179f08](https://github.com/MirrorNG/MirrorNG/commit/3179f08e3dc11340227a57da0104b1c8d1d7b45d))
* no longer requires hook to be the first overload in a class ([#1913](https://github.com/MirrorNG/MirrorNG/issues/1913)) ([0348699](https://github.com/MirrorNG/MirrorNG/commit/03486997fb0abb91d14f233658d375f21afbc3e3))
* OnClientEnterRoom should only fire on clients ([d9b7bb7](https://github.com/MirrorNG/MirrorNG/commit/d9b7bb735729e68ae399e1175d6c485a873b379e))
* Prevent host client redundantly changing to offline scene ([b4511a0](https://github.com/MirrorNG/MirrorNG/commit/b4511a0637958f10f4a482364c654d1d9add5ef2))
* Removed unnecessary registration of player prefab in NetworkRoomManager ([b2f52d7](https://github.com/MirrorNG/MirrorNG/commit/b2f52d78921ff0136c74bbed0980e3aaf5e2b379))
* Removed unused variable ([ae3dc04](https://github.com/MirrorNG/MirrorNG/commit/ae3dc04fb999c3b7133589ab631c1d23f1a8bdde))
* Replaced Icosphere with centered pivot ([1dc0d98](https://github.com/MirrorNG/MirrorNG/commit/1dc0d9837458c0403916476805f58442ff87e364))
* Replacing ClearDelegates with RemoveDelegates for test ([#1971](https://github.com/MirrorNG/MirrorNG/issues/1971)) ([927c4de](https://github.com/MirrorNG/MirrorNG/commit/927c4dede5930b320537150466e05112ebe70c3e))
* Suppress warning ([fffd462](https://github.com/MirrorNG/MirrorNG/commit/fffd462df8cc1c0265890cdfa4ceb3e24606b1c1))
* Use ReplaceHandler instead of RegisterHandler in NetworkManager ([ffc276c](https://github.com/MirrorNG/MirrorNG/commit/ffc276cb79c4202964275642097451b78a817c8a))
* Websockets Transport now handles being disabled for scene changes ([#1994](https://github.com/MirrorNG/MirrorNG/issues/1994)) ([5480a58](https://github.com/MirrorNG/MirrorNG/commit/5480a583e13b9183a3670450af639f4e766cc358))
* WebSockets: Force KeepAliveInterval to Zero ([9a42fe3](https://github.com/MirrorNG/MirrorNG/commit/9a42fe334251852ab12e721db72cb12e98de82e8))
* Wrong method names in ClientSceneTests ([ab3f353](https://github.com/MirrorNG/MirrorNG/commit/ab3f353b33b3068a6ac1649613a28b0977a72685))


### Features

* Add excludeOwner option to ClientRpc ([#1954](https://github.com/MirrorNG/MirrorNG/issues/1954)) ([864fdd5](https://github.com/MirrorNG/MirrorNG/commit/864fdd5fdce7a35ee4bf553176ed7a4ec3dc0653)), closes [#1963](https://github.com/MirrorNG/MirrorNG/issues/1963) [#1962](https://github.com/MirrorNG/MirrorNG/issues/1962) [#1961](https://github.com/MirrorNG/MirrorNG/issues/1961) [#1960](https://github.com/MirrorNG/MirrorNG/issues/1960) [#1959](https://github.com/MirrorNG/MirrorNG/issues/1959) [#1958](https://github.com/MirrorNG/MirrorNG/issues/1958) [#1957](https://github.com/MirrorNG/MirrorNG/issues/1957) [#1956](https://github.com/MirrorNG/MirrorNG/issues/1956)
* Add NetworkServer.RemovePlayerForConnection ([#1772](https://github.com/MirrorNG/MirrorNG/issues/1772)) ([e3790c5](https://github.com/MirrorNG/MirrorNG/commit/e3790c51eb9b79bebc48522fb832ae39f11d31e2))
* add SyncList.RemoveAll ([#1881](https://github.com/MirrorNG/MirrorNG/issues/1881)) ([eb7c87d](https://github.com/MirrorNG/MirrorNG/commit/eb7c87d15aa2fe0a5b0c08fc9cde0adbeba0b416))
* Added virtual SyncVar hook for index in NetworkRoomPlayer ([0c3e079](https://github.com/MirrorNG/MirrorNG/commit/0c3e079d04a034f4d4ca8a34c88188013f36c377))
* Adding ignoreAuthority Option to Command ([#1918](https://github.com/MirrorNG/MirrorNG/issues/1918)) ([3ace2c6](https://github.com/MirrorNG/MirrorNG/commit/3ace2c6eb68ad94d78c57df6f63107cca466effa))
* Adding onLocalPlayerChanged to ClientScene for when localPlayer is changed ([#1920](https://github.com/MirrorNG/MirrorNG/issues/1920)) ([b4acf7d](https://github.com/MirrorNG/MirrorNG/commit/b4acf7d9a20c05eadba8d433ebfd476a30e914dd))
* adding OnRoomServerPlayersNotReady to NetworkRoomManager that is called when player ready changes and atleast 1 player is not ready ([#1921](https://github.com/MirrorNG/MirrorNG/issues/1921)) ([9ae7fa2](https://github.com/MirrorNG/MirrorNG/commit/9ae7fa2a8c683f5d2a7ebe6c243a2d95acad9683))
* Adding ReplaceHandler functions to NetworkServer and NetworkClient ([#1775](https://github.com/MirrorNG/MirrorNG/issues/1775)) ([877f4e9](https://github.com/MirrorNG/MirrorNG/commit/877f4e9c729e5242d4f8c9653868a3cb27c933db))
* adding script that displays ping ([#1975](https://github.com/MirrorNG/MirrorNG/issues/1975)) ([7e93030](https://github.com/MirrorNG/MirrorNG/commit/7e93030849c98f0bc8d95fa310d208fef3028b29))
* Allowing Multiple Concurrent Additive Scenes ([#1697](https://github.com/MirrorNG/MirrorNG/issues/1697)) ([e32a9b6](https://github.com/MirrorNG/MirrorNG/commit/e32a9b6f0b0744b6bd0eeeb0d9fca0b4dc33cbdf))
* ClientScene uses log window ([b3656a9](https://github.com/MirrorNG/MirrorNG/commit/b3656a9edc5ff00329ce00847671ade7b8f2add2))
* Creating method to replace all log handlers ([#1880](https://github.com/MirrorNG/MirrorNG/issues/1880)) ([d8aaf76](https://github.com/MirrorNG/MirrorNG/commit/d8aaf76fb972dd153f6002edb96cd2b9350e572c))
* Experimental Network Transform ([#1990](https://github.com/MirrorNG/MirrorNG/issues/1990)) ([7e2b733](https://github.com/MirrorNG/MirrorNG/commit/7e2b7338a18855f156e52b663ac24eef153b43a7))
* Improved Log Settings Window Appearance ([#1885](https://github.com/MirrorNG/MirrorNG/issues/1885)) ([69b8451](https://github.com/MirrorNG/MirrorNG/commit/69b845183c099744455e34c6f12e75acecb9098a))
* Improved RoomPayer template ([042b4e1](https://github.com/MirrorNG/MirrorNG/commit/042b4e1965580a4cdbd5ea50b11a1377fe3bf3cc))
* LogSettings that can be saved and included in a build ([#1863](https://github.com/MirrorNG/MirrorNG/issues/1863)) ([fd4357c](https://github.com/MirrorNG/MirrorNG/commit/fd4357cd264b257aa648a26f9392726b2921b870))
* Multiple Concurrent Additive Physics Scenes Example ([#1686](https://github.com/MirrorNG/MirrorNG/issues/1686)) ([87c6ebc](https://github.com/MirrorNG/MirrorNG/commit/87c6ebc5ddf71b3fc358bb1a90bd9ee2470e333c))
* NetworkConnection to client and server use logger framework ([72154f1](https://github.com/MirrorNG/MirrorNG/commit/72154f1daddaa141fb3b8fe02bcfdf098ef1d44a))
* NetworkConnection uses logging framework ([ec319a1](https://github.com/MirrorNG/MirrorNG/commit/ec319a165dc8445b00b096d09061adda2c7b8b9d))
* NetworkIdentity use logger framework ([2e39e13](https://github.com/MirrorNG/MirrorNG/commit/2e39e13c012aa79d50a54fc5d07b85da3e52391b))
* NetworkServer uses new logging framework ([8b4f105](https://github.com/MirrorNG/MirrorNG/commit/8b4f1051f27f1d5b845e6bd0a090368082ab1603))
* Prettify Log Names ([c7d8c09](https://github.com/MirrorNG/MirrorNG/commit/c7d8c0933d37abc919305b660cbf3a57828eaace))
* Use SortedDictionary for LogSettings ([#1914](https://github.com/MirrorNG/MirrorNG/issues/1914)) ([7d4c0a9](https://github.com/MirrorNG/MirrorNG/commit/7d4c0a9cb6f24fa3c2834b9bf351e30dde88665f))


### Performance Improvements

* NetworkProximityChecker checks Server.connections instead of doing 10k sphere casts for 10k monsters. 2k NetworkTransforms demo is significantly faster. Stable 80fps instead of 500ms freezes in between. ([#1852](https://github.com/MirrorNG/MirrorNG/issues/1852)) ([2d89f05](https://github.com/MirrorNG/MirrorNG/commit/2d89f059afd9175dd7e6d81a0e2e38c0a28915c8))

## [34.9.4](https://github.com/MirrorNG/MirrorNG/compare/34.9.3-master...34.9.4-master) (2020-06-27)


### Bug Fixes

* Additive Scene Example was missing Player Auth on movement. ([#234](https://github.com/MirrorNG/MirrorNG/issues/234)) ([09bbd68](https://github.com/MirrorNG/MirrorNG/commit/09bbd686e6c294f24412b35785cfa7a5aa47b5f2))
* examples run in background ([#233](https://github.com/MirrorNG/MirrorNG/issues/233)) ([4755650](https://github.com/MirrorNG/MirrorNG/commit/47556500eed7c0e2719e41c0e996925ddf1799bb))

## [34.9.3](https://github.com/MirrorNG/MirrorNG/compare/34.9.2-master...34.9.3-master) (2020-06-25)


### Bug Fixes

* Optional Server or Client for PlayerSpawner ([#231](https://github.com/MirrorNG/MirrorNG/issues/231)) ([3fa5f89](https://github.com/MirrorNG/MirrorNG/commit/3fa5f89d8c934b233330efe52b42e59198a920cb))

## [34.9.2](https://github.com/MirrorNG/MirrorNG/compare/34.9.1-master...34.9.2-master) (2020-06-14)


### Bug Fixes

* Spawn Handler Order ([#223](https://github.com/MirrorNG/MirrorNG/issues/223)) ([8674274](https://github.com/MirrorNG/MirrorNG/commit/86742740ef2707f420d5cb6aeeb257bf07511f0b)), closes [#222](https://github.com/MirrorNG/MirrorNG/issues/222)

## [34.9.1](https://github.com/MirrorNG/MirrorNG/compare/34.9.0-master...34.9.1-master) (2020-05-24)


### Bug Fixes

* disconnect transport without domain reload ([20785b7](https://github.com/MirrorNG/MirrorNG/commit/20785b740e21fb22834cd01d7d628e127df6b80d))

# [34.9.0](https://github.com/MirrorNG/MirrorNG/compare/34.8.1-master...34.9.0-master) (2020-04-26)


### Bug Fixes

* Add the transport first so NetworkManager doesn't add Telepathy in OnValidate ([bdec276](https://github.com/MirrorNG/MirrorNG/commit/bdec2762821dc657e8450b576422fcf1f0f69cdf))
* call the virtual OnRoomServerDisconnect before the base ([e6881ef](https://github.com/MirrorNG/MirrorNG/commit/e6881ef007f199efca3c326ead258f0c350ffb47))
* compilation error on standalone build ([bb70bf9](https://github.com/MirrorNG/MirrorNG/commit/bb70bf963459be02a79c2c40cb7dfb8f10d2b92d))
* Removed NetworkClient.Update because NetworkManager does it in LateUpdate ([984945e](https://github.com/MirrorNG/MirrorNG/commit/984945e482529bfc03bf735562f3eff297a1bad4))
* Removed NetworkServer.Listen because HostSetup does that ([cf6823a](https://github.com/MirrorNG/MirrorNG/commit/cf6823acb5151d5bc9beca2b0911a99dfbcd4472))
* weaver syncLists now checks for SerializeItem in base class ([#1768](https://github.com/MirrorNG/MirrorNG/issues/1768)) ([1af5b4e](https://github.com/MirrorNG/MirrorNG/commit/1af5b4ed2f81b4450881fb11fa9b4b7e198274cb))


### Features

* Allow Multiple Network Animator ([#1778](https://github.com/MirrorNG/MirrorNG/issues/1778)) ([34a76a2](https://github.com/MirrorNG/MirrorNG/commit/34a76a2834cbeebb4c623f6650c1d67345b386ac))
* Allowing extra base types to be used for SyncLists and other SyncObjects ([#1729](https://github.com/MirrorNG/MirrorNG/issues/1729)) ([9bf816a](https://github.com/MirrorNG/MirrorNG/commit/9bf816a014fd393617422ee6efa52bdf730cc3c9))
* Disconnect Dead Clients ([#1724](https://github.com/MirrorNG/MirrorNG/issues/1724)) ([a2eb666](https://github.com/MirrorNG/MirrorNG/commit/a2eb666f158d72851d6c62997ed4b24dc3c473bc)), closes [#1753](https://github.com/MirrorNG/MirrorNG/issues/1753)
* Exclude fields from weaver's automatic Read/Write using System.NonSerialized attribute  ([#1727](https://github.com/MirrorNG/MirrorNG/issues/1727)) ([7f8733c](https://github.com/MirrorNG/MirrorNG/commit/7f8733ce6a8f712c195ab7a5baea166a16b52d09))
* Improve weaver error messages ([#1779](https://github.com/MirrorNG/MirrorNG/issues/1779)) ([bcd76c5](https://github.com/MirrorNG/MirrorNG/commit/bcd76c5bdc88af6d95a96e35d47b1b167d375652))
* NetworkServer.SendToReady ([#1773](https://github.com/MirrorNG/MirrorNG/issues/1773)) ([f6545d4](https://github.com/MirrorNG/MirrorNG/commit/f6545d4871bf6881b3150a3231af195e7f9eb8cd))

## [34.8.1](https://github.com/MirrorNG/MirrorNG/compare/34.8.0-master...34.8.1-master) (2020-04-21)


### Bug Fixes

* Allow sync objects to be re-used ([#1744](https://github.com/MirrorNG/MirrorNG/issues/1744)) ([58c89a3](https://github.com/MirrorNG/MirrorNG/commit/58c89a3d32daedc9b6670ed0b5eb1f8753c902e2)), closes [#1714](https://github.com/MirrorNG/MirrorNG/issues/1714)
* Remove leftover AddPlayer methods now that extraData is gone ([#1751](https://github.com/MirrorNG/MirrorNG/issues/1751)) ([2d006fe](https://github.com/MirrorNG/MirrorNG/commit/2d006fe7301eb8a0194af9ce9244988a6877f8f0))
* Remove RoomPlayer from roomSlots on Disconnect ([2a2f76c](https://github.com/MirrorNG/MirrorNG/commit/2a2f76c263093c342f307856e400aeabbedc58df))
* Use path instead of name in Room Example ([5d4bc47](https://github.com/MirrorNG/MirrorNG/commit/5d4bc47d46098f920f9e3468d0f276e336488e42))

# [34.8.0](https://github.com/MirrorNG/MirrorNG/compare/34.7.0-master...34.8.0-master) (2020-04-21)


### Bug Fixes

* Don't destroy the player twice ([#1709](https://github.com/MirrorNG/MirrorNG/issues/1709)) ([cbc2a47](https://github.com/MirrorNG/MirrorNG/commit/cbc2a4772921e01db17033075fa9f7d8cb7e6faf))
* Eliminate NetworkAnimator SetTrigger double firing on Host ([#1723](https://github.com/MirrorNG/MirrorNG/issues/1723)) ([e5b728f](https://github.com/MirrorNG/MirrorNG/commit/e5b728fed515ab679ad1e4581035d32f6c187a98))


### Features

* default log level option ([#1728](https://github.com/MirrorNG/MirrorNG/issues/1728)) ([5c56adc](https://github.com/MirrorNG/MirrorNG/commit/5c56adc1dc47ef91f7ee1d766cd70fa1681cb9df))
* NetworkMatchChecker Component ([#1688](https://github.com/MirrorNG/MirrorNG/issues/1688)) ([21acf66](https://github.com/MirrorNG/MirrorNG/commit/21acf661905ebc35f31a52eb527a50c6eff68a44)), closes [#1685](https://github.com/MirrorNG/MirrorNG/issues/1685) [#1681](https://github.com/MirrorNG/MirrorNG/issues/1681) [#1689](https://github.com/MirrorNG/MirrorNG/issues/1689)
* new virtual OnStopServer called when object is unspawned ([#1743](https://github.com/MirrorNG/MirrorNG/issues/1743)) ([d1695dd](https://github.com/MirrorNG/MirrorNG/commit/d1695dd16f477fc9edaaedb90032c188bcbba6e2))

# [34.7.0](https://github.com/MirrorNG/MirrorNG/compare/34.6.0-master...34.7.0-master) (2020-04-19)


### Features

* transport can provide their preferred scheme ([774a07e](https://github.com/MirrorNG/MirrorNG/commit/774a07e1bf26cce964cf14d502b71b43ce4f5cd0))

# [34.6.0](https://github.com/MirrorNG/MirrorNG/compare/34.5.0-master...34.6.0-master) (2020-04-19)


### Features

* onstopserver event in NetworkIdentity ([#186](https://github.com/MirrorNG/MirrorNG/issues/186)) ([eb81190](https://github.com/MirrorNG/MirrorNG/commit/eb8119007b19faca767969700b0209ade135650c))

# [34.5.0](https://github.com/MirrorNG/MirrorNG/compare/34.4.1-master...34.5.0-master) (2020-04-17)


### Features

* Added SyncList.Find and SyncList.FindAll ([#1716](https://github.com/MirrorNG/MirrorNG/issues/1716)) ([0fe6328](https://github.com/MirrorNG/MirrorNG/commit/0fe6328800daeef8680a19a394260295b7241442)), closes [#1710](https://github.com/MirrorNG/MirrorNG/issues/1710)
* Weaver can now automatically create Reader/Writer for types in a different assembly ([#1708](https://github.com/MirrorNG/MirrorNG/issues/1708)) ([b1644ae](https://github.com/MirrorNG/MirrorNG/commit/b1644ae481497d4347f404543c8200d2754617b9)), closes [#1570](https://github.com/MirrorNG/MirrorNG/issues/1570)


### Performance Improvements

* Adding dirty check before update sync var ([#1702](https://github.com/MirrorNG/MirrorNG/issues/1702)) ([58219c8](https://github.com/MirrorNG/MirrorNG/commit/58219c8f726cd65f8987c9edd747987057967ea4))

## [34.4.1](https://github.com/MirrorNG/MirrorNG/compare/34.4.0-master...34.4.1-master) (2020-04-15)


### Bug Fixes

* Fixing SyncVars not serializing when OnSerialize is overridden ([#1671](https://github.com/MirrorNG/MirrorNG/issues/1671)) ([c66c5a6](https://github.com/MirrorNG/MirrorNG/commit/c66c5a6dcc6837c840e6a51435b88fde10322297))

# [34.4.0](https://github.com/MirrorNG/MirrorNG/compare/34.3.0-master...34.4.0-master) (2020-04-14)


### Features

* Button to register all prefabs in NetworkClient ([#179](https://github.com/MirrorNG/MirrorNG/issues/179)) ([9f5f0b2](https://github.com/MirrorNG/MirrorNG/commit/9f5f0b27f8857bf55bf4f5ffbd436247f99cf390))

# [34.3.0](https://github.com/MirrorNG/MirrorNG/compare/34.2.0-master...34.3.0-master) (2020-04-13)


### Features

* Authenticators can now provide AuthenticationData ([310ce81](https://github.com/MirrorNG/MirrorNG/commit/310ce81c8378707e044108b94faac958e35cbca6))

# [34.2.0](https://github.com/MirrorNG/MirrorNG/compare/34.1.0-master...34.2.0-master) (2020-04-11)


### Features

* Use logger framework for NetworkClient ([#1685](https://github.com/MirrorNG/MirrorNG/issues/1685)) ([6e92bf5](https://github.com/MirrorNG/MirrorNG/commit/6e92bf5616d0d2486ce86497128094c4e33b5a3f))

# [34.1.0](https://github.com/MirrorNG/MirrorNG/compare/34.0.0-master...34.1.0-master) (2020-04-10)


### Bug Fixes

* Check SceneManager GetSceneByName and GetSceneByPath ([#1684](https://github.com/MirrorNG/MirrorNG/issues/1684)) ([e7cfd5a](https://github.com/MirrorNG/MirrorNG/commit/e7cfd5a498c7359636cd109fe586fce1771bada2))
* Re-enable transport if aborting additive load/unload ([#1683](https://github.com/MirrorNG/MirrorNG/issues/1683)) ([bc37497](https://github.com/MirrorNG/MirrorNG/commit/bc37497ac963bb0f2820b103591afd05177d078d))
* stack overflow getting logger ([55e075c](https://github.com/MirrorNG/MirrorNG/commit/55e075c872a076f524ec62f44d81df17819e81ba))


### Features

* logger factory works for static classes by passing the type ([f9328c7](https://github.com/MirrorNG/MirrorNG/commit/f9328c771cfb0974ce4765dc0d5af01440d838c0))


### Performance Improvements

* Increasing Network Writer performance ([#1674](https://github.com/MirrorNG/MirrorNG/issues/1674)) ([f057983](https://github.com/MirrorNG/MirrorNG/commit/f0579835ca52270de424e81691f12c02022c3909))

# [34.0.0](https://github.com/MirrorNG/MirrorNG/compare/33.1.1-master...34.0.0-master) (2020-04-10)


* remove NetworkConnection.isAuthenticated (#167) ([8a0e0b3](https://github.com/MirrorNG/MirrorNG/commit/8a0e0b3af37e8b0c74a8b97f12ec29cf202715ea)), closes [#167](https://github.com/MirrorNG/MirrorNG/issues/167)


### BREAKING CHANGES

* Remove isAuthenticated

* Fix typo

* Fix smells

* Remove smells

## [33.1.1](https://github.com/MirrorNG/MirrorNG/compare/33.1.0-master...33.1.1-master) (2020-04-09)


### Bug Fixes

* Invoke server.Disconnected before identity is removed for its conn ([#165](https://github.com/MirrorNG/MirrorNG/issues/165)) ([b749c4b](https://github.com/MirrorNG/MirrorNG/commit/b749c4ba126266a1799059f7fb407b6bcaa2bbd9))

# [33.1.0](https://github.com/MirrorNG/MirrorNG/compare/33.0.0-master...33.1.0-master) (2020-04-08)


### Features

* new websocket transport ([#156](https://github.com/MirrorNG/MirrorNG/issues/156)) ([23c7b0d](https://github.com/MirrorNG/MirrorNG/commit/23c7b0d1b32684d4f959495fe96b2d16a68bd356))

# [33.0.0](https://github.com/MirrorNG/MirrorNG/compare/32.0.1-master...33.0.0-master) (2020-04-08)


* Simplify RegisterHandler (#160) ([f4f5167](https://github.com/MirrorNG/MirrorNG/commit/f4f516791b8390f0babf8a7aefa19254427d4145)), closes [#160](https://github.com/MirrorNG/MirrorNG/issues/160)


### BREAKING CHANGES

* NetworkConneciton.RegisterHandler only needs message class

## [32.0.1](https://github.com/MirrorNG/MirrorNG/compare/32.0.0-master...32.0.1-master) (2020-04-08)


### Performance Improvements

* Use continuewith to queue up ssl messages ([#1640](https://github.com/MirrorNG/MirrorNG/issues/1640)) ([84b2c8c](https://github.com/MirrorNG/MirrorNG/commit/84b2c8cf2671728baecf734487ddaa7fab9943a0))

# [32.0.0](https://github.com/MirrorNG/MirrorNG/compare/31.4.0-master...32.0.0-master) (2020-04-07)


* Remove NetworkConnectionToClient (#155) ([bd95cea](https://github.com/MirrorNG/MirrorNG/commit/bd95cea4d639753335b21c48781603acd758a9e7)), closes [#155](https://github.com/MirrorNG/MirrorNG/issues/155)


### BREAKING CHANGES

* NetworkConnectionToClient and networkConnectionToServer are gone

# [31.4.0](https://github.com/MirrorNG/MirrorNG/compare/31.3.1-master...31.4.0-master) (2020-04-04)


### Bug Fixes

* disconnect even if there is an exception ([#152](https://github.com/MirrorNG/MirrorNG/issues/152)) ([2eb9de6](https://github.com/MirrorNG/MirrorNG/commit/2eb9de6b470579b6de75853ba161b3e7a36de698))


### Features

* spawning invalid object now gives exception ([e2fc829](https://github.com/MirrorNG/MirrorNG/commit/e2fc8292400aae8b3b8b972ff5824b8d9cdd6b88))

## [31.3.1](https://github.com/MirrorNG/MirrorNG/compare/31.3.0-master...31.3.1-master) (2020-04-03)


### Performance Improvements

* Adding buffer for local connection ([#1621](https://github.com/MirrorNG/MirrorNG/issues/1621)) ([4d5cee8](https://github.com/MirrorNG/MirrorNG/commit/4d5cee893d0104c0070a0b1814c8c84f11f24f18))

# [31.3.0](https://github.com/MirrorNG/MirrorNG/compare/31.2.1-master...31.3.0-master) (2020-04-01)


### Bug Fixes

* Destroyed NetMan due to singleton collision must not continue. ([#1636](https://github.com/MirrorNG/MirrorNG/issues/1636)) ([d2a58a4](https://github.com/MirrorNG/MirrorNG/commit/d2a58a4c251c97cdb38c88c9a381496b3078adf8))


### Features

* logging api ([#1611](https://github.com/MirrorNG/MirrorNG/issues/1611)) ([f2ccb59](https://github.com/MirrorNG/MirrorNG/commit/f2ccb59ae6db90bc84f8a36802bfe174b4493127))


### Performance Improvements

* faster NetworkReader pooling ([#1623](https://github.com/MirrorNG/MirrorNG/issues/1623)) ([1ae0381](https://github.com/MirrorNG/MirrorNG/commit/1ae038172ac7f5e18e0e09b0081f7f42fa0eff7a))

## [31.2.1](https://github.com/MirrorNG/MirrorNG/compare/31.2.0-master...31.2.1-master) (2020-04-01)


### Bug Fixes

* pass the correct connection to TargetRpcs ([#146](https://github.com/MirrorNG/MirrorNG/issues/146)) ([9df2f79](https://github.com/MirrorNG/MirrorNG/commit/9df2f798953f78de113ef6fa9fea111b03a52cd0))

# [31.2.0](https://github.com/MirrorNG/MirrorNG/compare/31.1.0-master...31.2.0-master) (2020-04-01)


### Features

* Add fallback transport ([1b02796](https://github.com/MirrorNG/MirrorNG/commit/1b02796c1468c1e1650eab0f278cd9a11cc597c7))

# [31.1.0](https://github.com/MirrorNG/MirrorNG/compare/31.0.0-master...31.1.0-master) (2020-04-01)


### Features

* async multiplex transport ([#145](https://github.com/MirrorNG/MirrorNG/issues/145)) ([c0e7e92](https://github.com/MirrorNG/MirrorNG/commit/c0e7e9280931a5996f595e41aa516bef20208b6f))

# [31.0.0](https://github.com/MirrorNG/MirrorNG/compare/30.3.3-master...31.0.0-master) (2020-04-01)


### Bug Fixes

* chat example ([e6e10a7](https://github.com/MirrorNG/MirrorNG/commit/e6e10a7108bc01e3bd0c208734c97c945003ff86))
* missing meta ([87ace4d](https://github.com/MirrorNG/MirrorNG/commit/87ace4dda09331968cc9d0185ce1de655f5dfb15))


### Features

* asynchronous transport ([#134](https://github.com/MirrorNG/MirrorNG/issues/134)) ([0e84f45](https://github.com/MirrorNG/MirrorNG/commit/0e84f451e822fe7c1ca1cd04e052546ed273cfce))


### BREAKING CHANGES

* connecition Id is gone
* websocket transport does not work,  needs to be replaced.

## [30.3.3](https://github.com/MirrorNG/MirrorNG/compare/30.3.2-master...30.3.3-master) (2020-03-31)


### Bug Fixes

* headless build ([7864e8d](https://github.com/MirrorNG/MirrorNG/commit/7864e8d6f4a0952ef3114daac11842e4ee0a7a58))
* headless build ([ab47a45](https://github.com/MirrorNG/MirrorNG/commit/ab47a45d08f4e9a82a5cd26f913f4871d46dd484))

## [30.3.2](https://github.com/MirrorNG/MirrorNG/compare/30.3.1-master...30.3.2-master) (2020-03-31)


### Bug Fixes

* AsyncTcp now exits normally when client disconnects ([#141](https://github.com/MirrorNG/MirrorNG/issues/141)) ([8896c4a](https://github.com/MirrorNG/MirrorNG/commit/8896c4afa2f937839a54dc71fbe578b9c636f393))

## [30.3.1](https://github.com/MirrorNG/MirrorNG/compare/30.3.0-master...30.3.1-master) (2020-03-30)


### Bug Fixes

* reset buffer every time ([a8a62a6](https://github.com/MirrorNG/MirrorNG/commit/a8a62a64b6fa67223505505c1225269d3a047a92))

# [30.3.0](https://github.com/MirrorNG/MirrorNG/compare/30.2.0-master...30.3.0-master) (2020-03-30)


### Features

* Piped connection ([#138](https://github.com/MirrorNG/MirrorNG/issues/138)) ([471a881](https://github.com/MirrorNG/MirrorNG/commit/471a881cdae1c6e526b5aa2d552cc91dc27f793a))

# [30.2.0](https://github.com/MirrorNG/MirrorNG/compare/30.1.2-master...30.2.0-master) (2020-03-30)


### Features

* allow more than one NetworkManager ([#135](https://github.com/MirrorNG/MirrorNG/issues/135)) ([92968e4](https://github.com/MirrorNG/MirrorNG/commit/92968e4e45a33fa5ab77ce2bfc9e8f826a888711))

## [30.1.2](https://github.com/MirrorNG/MirrorNG/compare/30.1.1-master...30.1.2-master) (2020-03-29)


### Bug Fixes

* client being disconnected twice ([#132](https://github.com/MirrorNG/MirrorNG/issues/132)) ([36bb3a2](https://github.com/MirrorNG/MirrorNG/commit/36bb3a2418bcf41fd63d1fc79e8a5173e4b0bc51))
* client disconnected on server event never raised ([#133](https://github.com/MirrorNG/MirrorNG/issues/133)) ([9d9efa0](https://github.com/MirrorNG/MirrorNG/commit/9d9efa0e31cbea4d90d7408ae6b3742151b49a21))

## [30.1.1](https://github.com/MirrorNG/MirrorNG/compare/30.1.0-master...30.1.1-master) (2020-03-29)


### Performance Improvements

* faster NetworkWriter pooling ([#1620](https://github.com/MirrorNG/MirrorNG/issues/1620)) ([4fa43a9](https://github.com/MirrorNG/MirrorNG/commit/4fa43a947132f89e5348c63e393dd3b80e1fe7e1))

# [30.1.0](https://github.com/MirrorNG/MirrorNG/compare/30.0.0-master...30.1.0-master) (2020-03-29)


### Features

* allow Play mode options ([f9afb64](https://github.com/MirrorNG/MirrorNG/commit/f9afb6407b015c239975c5a1fba6609e12ab5c8f))

# [30.0.0](https://github.com/MirrorNG/MirrorNG/compare/29.1.1-master...30.0.0-master) (2020-03-29)


### Features

* Server raises an event when it starts ([#126](https://github.com/MirrorNG/MirrorNG/issues/126)) ([d5b0a6f](https://github.com/MirrorNG/MirrorNG/commit/d5b0a6f0dd65f9dbb6c4848bce5e81f93772a235))


### BREAKING CHANGES

* NetworkManager no longer has OnServerStart virtual

## [29.1.1](https://github.com/MirrorNG/MirrorNG/compare/29.1.0-master...29.1.1-master) (2020-03-29)


### Reverts

* Revert "Revert "Explain why 10"" ([d727e4f](https://github.com/MirrorNG/MirrorNG/commit/d727e4fd4b9e811025c7309efeba090e3ac14ccd))

# [29.1.0](https://github.com/MirrorNG/MirrorNG/compare/29.0.3-master...29.1.0-master) (2020-03-28)


### Features

* get a convenient property to get network time ([1e8c2fe](https://github.com/MirrorNG/MirrorNG/commit/1e8c2fe0522d7843a6a2fae7287287c7afa4e417))

## [29.0.3](https://github.com/MirrorNG/MirrorNG/compare/29.0.2-master...29.0.3-master) (2020-03-28)


### Performance Improvements

* faster NetworkWriter pooling ([#1616](https://github.com/MirrorNG/MirrorNG/issues/1616)) ([5128b12](https://github.com/MirrorNG/MirrorNG/commit/5128b122fe205f250d44ba5c7a88a50de2f3e4cd)), closes [#1614](https://github.com/MirrorNG/MirrorNG/issues/1614)
* replace isValueType with faster alternative ([#1617](https://github.com/MirrorNG/MirrorNG/issues/1617)) ([61163ca](https://github.com/MirrorNG/MirrorNG/commit/61163cacb4cb2652aa8632f84be89212674436ff)), closes [/github.com/vis2k/Mirror/issues/1614#issuecomment-605443808](https://github.com//github.com/vis2k/Mirror/issues/1614/issues/issuecomment-605443808)
* use byte[] directly instead of MemoryStream ([#1618](https://github.com/MirrorNG/MirrorNG/issues/1618)) ([166b8c9](https://github.com/MirrorNG/MirrorNG/commit/166b8c946736447a76c1886c4d1fb036f6e56e20))

## [29.0.2](https://github.com/MirrorNG/MirrorNG/compare/29.0.1-master...29.0.2-master) (2020-03-27)


### Performance Improvements

* Remove redundant mask ([#1604](https://github.com/MirrorNG/MirrorNG/issues/1604)) ([5d76afb](https://github.com/MirrorNG/MirrorNG/commit/5d76afbe29f456a657c9e1cb7c97435242031091))

## [29.0.1](https://github.com/MirrorNG/MirrorNG/compare/29.0.0-master...29.0.1-master) (2020-03-27)


### Bug Fixes

* [#1515](https://github.com/MirrorNG/MirrorNG/issues/1515) - StopHost now invokes OnServerDisconnected for the host client too ([#1601](https://github.com/MirrorNG/MirrorNG/issues/1601)) ([678ac68](https://github.com/MirrorNG/MirrorNG/commit/678ac68b58798816658d29be649bdaf18ad70794))


### Performance Improvements

* simplify and speed up getting methods in weaver ([c1cfc42](https://github.com/MirrorNG/MirrorNG/commit/c1cfc421811e4c12e84cb28677ac68c82575958d))

# [29.0.0](https://github.com/MirrorNG/MirrorNG/compare/28.0.0-master...29.0.0-master) (2020-03-26)


### Features

* PlayerSpawner component ([#123](https://github.com/MirrorNG/MirrorNG/issues/123)) ([e8b933d](https://github.com/MirrorNG/MirrorNG/commit/e8b933ddff9a47b64be371edb63af130bd3958b4))


### BREAKING CHANGES

* NetworkManager no longer spawns the player.  You need to add PlayerSpawner component if you want that behavior

# [28.0.0](https://github.com/MirrorNG/MirrorNG/compare/27.0.1-master...28.0.0-master) (2020-03-26)


### Bug Fixes

* [#1599](https://github.com/MirrorNG/MirrorNG/issues/1599) - NetworkManager HUD calls StopHost/Server/Client depending on state. It does not call StopHost in all cases. ([#1600](https://github.com/MirrorNG/MirrorNG/issues/1600)) ([8c6ae0f](https://github.com/MirrorNG/MirrorNG/commit/8c6ae0f8b4fdafbc3abd194c081c75ee75fcfe51))


### Features

* now you can assign scenes even if not in Editor ([#1576](https://github.com/MirrorNG/MirrorNG/issues/1576)) ([c8a1a5e](https://github.com/MirrorNG/MirrorNG/commit/c8a1a5e56f7561487e3180f26e28484f714f36c1))


### BREAKING CHANGES

* You will need to reassign your scenes after upgrade

* Automatically fix properties that were using name

If you open a NetworkManager or other gameobject that uses a scene name
it now gets converted to scene path by the SceneDrawer

* Use get scene by name

* Scene can never be null

* Update Assets/Mirror/Examples/AdditiveScenes/Scenes/MainScene.unity

* Issue warning if we drop the scene

* Issue error if scene is lost

* Add suggestion on how to fix the error

* Keep backwards compatibility, check for scene name

* cache the active scene

* Update Assets/Mirror/Editor/SceneDrawer.cs

Co-Authored-By: James Frowen <jamesfrowendev@gmail.com>

* GetSceneByName only works if scene is loaded

* Remove unused using

Co-authored-by: James Frowen <jamesfrowendev@gmail.com>

## [27.0.1](https://github.com/MirrorNG/MirrorNG/compare/27.0.0-master...27.0.1-master) (2020-03-26)


### Bug Fixes

* empty scene name isn't considered as empty ([ec3a939](https://github.com/MirrorNG/MirrorNG/commit/ec3a93945b5b52a77fd745b39e1e821db721768d))

# [27.0.0](https://github.com/MirrorNG/MirrorNG/compare/26.0.0-master...27.0.0-master) (2020-03-26)


* remove room feature for now (#122) ([87dd495](https://github.com/MirrorNG/MirrorNG/commit/87dd495a6fca6c85349afd42ba6449d98de1f567)), closes [#122](https://github.com/MirrorNG/MirrorNG/issues/122)
* Server Disconnect is now an event not a message (#121) ([82ebd71](https://github.com/MirrorNG/MirrorNG/commit/82ebd71456cbd2e819540d961a93814c57735784)), closes [#121](https://github.com/MirrorNG/MirrorNG/issues/121)


### Code Refactoring

* Remove offline/online scenes ([#120](https://github.com/MirrorNG/MirrorNG/issues/120)) ([a4c881a](https://github.com/MirrorNG/MirrorNG/commit/a4c881a36e26b20fc72166741e20c84ce030ad8f))


### BREAKING CHANGES

* Room feature and example are gone
* offline/online scenes are gone
* OnServerDisconnect is gone

# [26.0.0](https://github.com/MirrorNG/MirrorNG/compare/25.0.0-master...26.0.0-master) (2020-03-25)


* remove OnClientStart virtual ([eb5242d](https://github.com/MirrorNG/MirrorNG/commit/eb5242d63fa011381e7692470713fd144476454a))


### BREAKING CHANGES

* Removed OnStartClient virtual,  use event from NetworkClient instead

# [25.0.0](https://github.com/MirrorNG/MirrorNG/compare/24.1.1-master...25.0.0-master) (2020-03-25)


* Move on client stop (#118) ([678e386](https://github.com/MirrorNG/MirrorNG/commit/678e3867a9f232e52d2a6cdbfae8140b0e82bd11)), closes [#118](https://github.com/MirrorNG/MirrorNG/issues/118)


### Features

* Added Virtual OnRoomStopServer to NetworkRoomManager and Script Template ([d034ef6](https://github.com/MirrorNG/MirrorNG/commit/d034ef616f3d479729064d652f74a905ea05b495))


### BREAKING CHANGES

* OnStopClient virtual is replaced by event in Client

## [24.1.1](https://github.com/MirrorNG/MirrorNG/compare/24.1.0-master...24.1.1-master) (2020-03-25)


### Bug Fixes

* [#1593](https://github.com/MirrorNG/MirrorNG/issues/1593) - NetworkRoomManager.ServerChangeScene doesn't destroy the world player before replacing the connection. otherwise ReplacePlayerForConnection removes authority form a destroyed object, causing all kidns of errors. The call wasn't actually needed. ([#1594](https://github.com/MirrorNG/MirrorNG/issues/1594)) ([347cb53](https://github.com/MirrorNG/MirrorNG/commit/347cb5374d0cba72762e893645f076d3161aa0c5))

# [24.1.0](https://github.com/MirrorNG/MirrorNG/compare/24.0.1-master...24.1.0-master) (2020-03-24)


### Features

* connections can retrieve end point ([#114](https://github.com/MirrorNG/MirrorNG/issues/114)) ([d239718](https://github.com/MirrorNG/MirrorNG/commit/d239718498c5750edf0b5d11cc762136f45500fd))
* transports can give server uri ([#113](https://github.com/MirrorNG/MirrorNG/issues/113)) ([dc700ec](https://github.com/MirrorNG/MirrorNG/commit/dc700ec721cf4ecf6ddd082d88b933c9afffbc67))

## [24.0.1](https://github.com/MirrorNG/MirrorNG/compare/24.0.0-master...24.0.1-master) (2020-03-23)


### Bug Fixes

* Default port is 7777 ([960c39d](https://github.com/MirrorNG/MirrorNG/commit/960c39da90db156cb58d4765695664f0c084b39a))

# [24.0.0](https://github.com/MirrorNG/MirrorNG/compare/23.0.0-master...24.0.0-master) (2020-03-23)


### Features

* individual events for SyncDictionary ([#112](https://github.com/MirrorNG/MirrorNG/issues/112)) ([b3c1b16](https://github.com/MirrorNG/MirrorNG/commit/b3c1b16100c440131d6d933627a9f6479aed11ad))


### BREAKING CHANGES

* SyncDictionary callback has been replaced

# [23.0.0](https://github.com/MirrorNG/MirrorNG/compare/22.0.0-master...23.0.0-master) (2020-03-23)


### Features

* individual events for SyncSet ([#111](https://github.com/MirrorNG/MirrorNG/issues/111)) ([261f5d6](https://github.com/MirrorNG/MirrorNG/commit/261f5d6a1481634dc524fb57b5866e378a1d909d))


### BREAKING CHANGES

* callback signature changed in SyncSet

# [22.0.0](https://github.com/MirrorNG/MirrorNG/compare/21.2.1-master...22.0.0-master) (2020-03-23)


### Features

* synclists has individual meaningful events ([#109](https://github.com/MirrorNG/MirrorNG/issues/109)) ([e326064](https://github.com/MirrorNG/MirrorNG/commit/e326064b51e8372726b30d19973df6293c74c376)), closes [#103](https://github.com/MirrorNG/MirrorNG/issues/103)


### BREAKING CHANGES

* Sync lists no longer have a Callback event with an operation enum

## [21.2.1](https://github.com/MirrorNG/MirrorNG/compare/21.2.0-master...21.2.1-master) (2020-03-23)


### Bug Fixes

* overriden hooks are invoked (fixes [#1581](https://github.com/MirrorNG/MirrorNG/issues/1581)) ([#1584](https://github.com/MirrorNG/MirrorNG/issues/1584)) ([cf55333](https://github.com/MirrorNG/MirrorNG/commit/cf55333a072c0ffe36a2972ca0a5122a48d708d0))

# [21.2.0](https://github.com/MirrorNG/MirrorNG/compare/21.1.0-master...21.2.0-master) (2020-03-23)


### Features

* next gen async transport ([#106](https://github.com/MirrorNG/MirrorNG/issues/106)) ([4a8dc67](https://github.com/MirrorNG/MirrorNG/commit/4a8dc676b96840493d178718049b9e20c8eb6510))

# [21.1.0](https://github.com/MirrorNG/MirrorNG/compare/21.0.1-master...21.1.0-master) (2020-03-22)


### Features

* NetworkConnection manages messages handlers ([#93](https://github.com/MirrorNG/MirrorNG/issues/93)) ([5c79f0d](https://github.com/MirrorNG/MirrorNG/commit/5c79f0db64e46905081e6c0b5502376c5acf0d99))

## [21.0.1](https://github.com/MirrorNG/MirrorNG/compare/21.0.0-master...21.0.1-master) (2020-03-22)


### Bug Fixes

* calling Connect and Authenticate handler twice ([#102](https://github.com/MirrorNG/MirrorNG/issues/102)) ([515f5a1](https://github.com/MirrorNG/MirrorNG/commit/515f5a15cd5be984f8cb4f94d3be0a0ac919eb63))

# [21.0.0](https://github.com/MirrorNG/MirrorNG/compare/20.1.0-master...21.0.0-master) (2020-03-22)


### Features

* NetworkIdentity lifecycle events ([#88](https://github.com/MirrorNG/MirrorNG/issues/88)) ([9a7c572](https://github.com/MirrorNG/MirrorNG/commit/9a7c5726eb3d333b85c3d0e44b884c11e34be45d))


### BREAKING CHANGES

* NetworkBehavior no longer has virtuals for lifecycle events

# [20.1.0](https://github.com/MirrorNG/MirrorNG/compare/20.0.6-master...20.1.0-master) (2020-03-22)


### Bug Fixes

* tcp server Tests ([3b95477](https://github.com/MirrorNG/MirrorNG/commit/3b954777f16a41469d953e3744c5d68554e3d200))


### Features

* NetworkClient raises event after authentication ([#96](https://github.com/MirrorNG/MirrorNG/issues/96)) ([c332271](https://github.com/MirrorNG/MirrorNG/commit/c332271d918f782d4b1a84b3f0fd660239f95743))

## [20.0.6](https://github.com/MirrorNG/MirrorNG/compare/20.0.5-master...20.0.6-master) (2020-03-22)


### Bug Fixes

* NetworkConnectionEvent should be serialized in editor ([0e756ce](https://github.com/MirrorNG/MirrorNG/commit/0e756cec06c5fda9eb4b5c8aa9de093c32b0315c))

## [20.0.5](https://github.com/MirrorNG/MirrorNG/compare/20.0.4-master...20.0.5-master) (2020-03-21)


### Bug Fixes

* Added LogFilter.Debug check in a few places ([#1575](https://github.com/MirrorNG/MirrorNG/issues/1575)) ([3156504](https://github.com/MirrorNG/MirrorNG/commit/31565042708ec768fcaafe9986968d095a3a1419))
* comment punctuation ([4d827cd](https://github.com/MirrorNG/MirrorNG/commit/4d827cd9f60e4fb7cd6524d78ca26ea1d88a1eff))
* Make SendToReady non-ambiguous ([#1578](https://github.com/MirrorNG/MirrorNG/issues/1578)) ([b627779](https://github.com/MirrorNG/MirrorNG/commit/b627779acd68b2acfcaf5eefc0d3864dcce57fc7))

## [20.0.4](https://github.com/MirrorNG/MirrorNG/compare/20.0.3-master...20.0.4-master) (2020-03-21)


### Bug Fixes

* movement in room demo ([49f7904](https://github.com/MirrorNG/MirrorNG/commit/49f7904b4a83fc5318031d273cb10a4b87af2172))

## [20.0.3](https://github.com/MirrorNG/MirrorNG/compare/20.0.2-master...20.0.3-master) (2020-03-21)


### Bug Fixes

* additive scene player movement is client authoritative ([e683a92](https://github.com/MirrorNG/MirrorNG/commit/e683a92b081c989314c11e48fb21ee0096465797))
* the Room scene references other scenes ([9b60871](https://github.com/MirrorNG/MirrorNG/commit/9b60871e2ea1a2912c0af3d95796660fc04dc569))

## [20.0.2](https://github.com/MirrorNG/MirrorNG/compare/20.0.1-master...20.0.2-master) (2020-03-21)


### Bug Fixes

* additive scene example ([9fa0169](https://github.com/MirrorNG/MirrorNG/commit/9fa016957f487526ab44d443aabfe58fcc69518a))

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
