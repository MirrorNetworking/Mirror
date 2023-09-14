Very simple multiplayer Billiards demo.
Mouse drag the white ball to apply force.

Billiards is surprisingly easy to implement, which makes this a great demo for beginners!

Hits are sent to the server with a [Command].
There will always be some latency for the results to show.

To solve this, there's another BilliardsPredicted demo which uses prediction & reconciliation.
This demo however is meant for complete beginners to learn Mirror!

Notes:
- Red/White ball Rigidbody CollisionMode needs to be ContinousDynamic to avoid white flying through red sometimes.
  even 'Continous' is not enough, we need ContinousDynamic.