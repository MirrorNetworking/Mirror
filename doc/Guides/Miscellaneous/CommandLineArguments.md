# Command Line Arguments


## -No-Weave

Adding the `-no-weave` command line argument will stop weaver from processing dlls. 

This can be useful if you need to run `executeMethod` with static fields. Without `-no-weave` script will reload and any static fields are lost.

See these issues for more:
- https://github.com/vis2k/Mirror/pull/2209
- https://github.com/vis2k/Mirror/issues/1319