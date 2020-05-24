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
