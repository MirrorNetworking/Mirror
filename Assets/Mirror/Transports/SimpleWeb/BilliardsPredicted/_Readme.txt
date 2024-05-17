Advanced multiplayer Billiards demo with Prediction.

Please read this first:
https://mirror-networking.gitbook.io/docs/manual/general/client-side-prediction

Mouse drag the white ball to apply force.
PredictedRigidbody syncInterval is intentionally set pretty high so we can see when it corrects.

If you are a beginner, start with the basic Billiards demo instead.
If you are advanced, this demo shows how to use Mirror's prediction features for physics / FPS games.

Billiards is a great example to try our Prediction algorithm, it works extremely well here!

=> We use 'Fast' Prediction mode for Billiards because we want to see exact collisions with balls/walls.
=> 'Smooth' mode would look too soft, with balls changing direction even before touching other balls/walls.

Notes:
- Red/White ball Rigidbody CollisionMode needs to be ContinousDynamic to avoid white flying through red sometimes.
  even 'Continous' is not enough, we need ContinousDynamic.
