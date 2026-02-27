# Mirror Networking – Copilot Instructions

## Project
Unity networking library (Mirror) located at `Assets/Mirror/`.

## Key directory layout
- `Assets/Mirror/Core/` — networking core (NetworkServer, NetworkClient, NetworkIdentity, NetworkBehaviour, transports, sync primitives)
- `Assets/Mirror/Components/` — MonoBehaviour components (NetworkTransform, NetworkAnimator, NetworkRigidbody, PredictedRigidbody, LagCompensator, Discovery, Profiling)
- `Assets/Mirror/Transports/` — KCP, Telepathy, SimpleWeb, Multiplex, Middleware, Latency, Encryption
- `Assets/Mirror/Editor/` — Unity editor scripts and IL Weaver (Weaver, processors for SyncVars/Commands/RPCs)
- `Assets/Mirror/Authenticators/` — BasicAuthenticator, DeviceAuthenticator
- `Assets/Mirror/Tests/` — unit/integration tests (excluded from production builds)
- `Assets/Mirror/Examples/` — demo scenes (excluded from production builds)
- `Assets/Mirror/Hosting/` — Edgegap hosting integration (excluded from production builds)

## Coding conventions
- C# with Unity-style naming (PascalCase types/methods, camelCase fields)
- Private fields must follow camelCase naming as enforced by the `.editorconfig` (e.g., connectingSendQueue, not ConnectingSendQueue).
- Static classes for core systems (NetworkServer, NetworkClient)
- IL post-processing via Weaver for [SyncVar], [Command], [ClientRpc] attributes
- `.editorconfig` at repo root enforces: Allman braces, no `var`, camelCase private fields, no `this.` qualification
