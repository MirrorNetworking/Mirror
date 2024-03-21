Advanced multiplayer Billiards demo with Prediction.
Mouse drag the white ball to apply force.
PredictedRigidbody syncInterval is intentionally set pretty high so we can see when it corrects.

If you are a beginner, start with the basic Billiards demo instead.
If you are advanced, this demo shows how to use Mirror's prediction features for physics / FPS games.

Billiards is a great example to try our Prediction algorithm, it works extremely well here!

The only part we don't love yet is this:
- each ball has a PredictedRigidbody component and Rigidbody+Collider
- while predicting, PredictedRigibody sometimes moves the Rigidbody+Collider onto a temporary ghost object
- this works well, the only downside is that OnTriggerEnter etc. won't work on the main object while there's no collider.
=> we are looking to make this part easier in the future

Notes:
- Red/White ball Rigidbody CollisionMode needs to be ContinousDynamic to avoid white flying through red sometimes.
  even 'Continous' is not enough, we need ContinousDynamic.
