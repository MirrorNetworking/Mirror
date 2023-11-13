Advanced multiplayer Billiards demo with Prediction.
Mouse drag the white ball to apply force.
PredictedRigidbody syncInterval is intentionally set pretty high so we can see when it corrects.

If you are a beginner, start with the basic Billiards demo instead.
If you are advanced, this demo shows how to use Mirror's prediction features for physics / FPS games.

The demo is work in progress.
At the moment, this is only for the Mirror team to test individual prediction features!

Notes:
- Red/White ball Rigidbody CollisionMode needs to be ContinousDynamic to avoid white flying through red sometimes.
  even 'Continous' is not enough, we need ContinousDynamic.
