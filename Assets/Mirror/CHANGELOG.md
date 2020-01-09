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
