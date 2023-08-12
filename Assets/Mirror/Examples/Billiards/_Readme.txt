Very simple multiplayer Billiards demo.
Mouse drag the white ball to apply force.

Billiards is surprisingly easy to implement, which makes this a great demo for beginners!

Additionally, this demo will allow us to test Client Side Prediction & Reconciliation:
- currently, CmdApplyForce is sent to server, and clients see the effect a bit later (latency)
- in the future, prediction will show the effect immediately with (ideally) very little corrections

The demo is intentionally kept extremely simple without any rules.
This way we can apply force and test physics without much wait time.
