This example is used to stabilize our prediction algorithm for stacked Rigidbodies.

It's important to understand that there are two problems here:

1. Stacking Rigidbodies with Unity physics.
   This is difficult even in single player mode.
   https://forum.unity.com/threads/stacking-boxes-issue.1341128/
   => with solverIterations=100 we can stack about 500 cubes at max.

2. Networked Prediction for stacked Rigidbodies.
   This is even harder since Rigidbodies may need to be corrected going through each other.

==> This demo is NOT READY for users or for production games.
==> For now, this is only for the Mirror team to debug prediction.
==> Note that client cubes may change color if PredictedRigidbody.showRemoteSleeping is enabled.

DO NOT USE THIS
