Our Shooter Example aims to replicate fast paced first person shooter games like Quake & Counter-Strike,
by utilizing Mirror's advanced features:

- Snapshot Interpolation: this gives us perfectly smooth movement sync.
- Lag Compensation: accurate hit detection that goes back in time to compensate for latency.
- Client Side Prediction: immediate feedback when shooting interactable objects, with server corrections if needed.
- Two Click Hosting: host a server with two clicks directly from the Unity Editor.

The goal is to provide a lightweight demo that users can learn from.
All the complicated magic should be hidden in Mirror Components, with minimal custom code.
We want users to understand this easily and implement it in their own projects with very little effort.