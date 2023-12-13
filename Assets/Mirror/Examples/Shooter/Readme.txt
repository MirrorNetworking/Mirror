Our Shooter Example aims to replicate fast paced first person shooter games like Quake & Counter-Strike,
by utilizing Mirror's advanced features:

- Snapshot Interpolation: this gives us perfectly smooth movement sync.
- Lag Compensation: accurate hit detection that goes back in time to compensate for latency.
- Client Side Prediction: immediate feedback when shooting interactable objects, with server corrections if needed.
- Two Click Hosting: host a server with two clicks directly from the Unity Editor.
- Character Controller 2k: our free open source collide & slide character controller.
  https://github.com/MirrorNetworking/CharacterController2k
  The Unity built in controller doesn't support sliding down slopes.
  And for deterministic physics, we'll need an open controller anyway.

The goal is to provide a lightweight demo that users can learn from.
All the complicated magic should be hidden in Mirror Components, with minimal custom code.
We want users to understand this easily and implement it in their own projects with very little effort.