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
