# Write Transform benchmark

Trying different approaches to see how to best write Transform state to balance for cpu and bandwidth

## Results 

### Position
PositionCompression bits:34
bounds 0 -> (200, 50, 200),
precision 0.05f

Method                  | time(ms) | Bytes
------------------------|----------|-------
WritePositions_Blitable |  121     | 12000
CompressedPositions     | 1355     |  5000
PackPositions           | 1326     |  4250

### Rotation

Method                                    | time(ms) | Bytes
------------------------------------------|----------|-------
WriteRotations_Blitable                   |  114     | 16000
CompressedRotations                       | 3478     |  4000
PackRotations_Length9                     | 3687     |  3625
PackRotations_Length10                    | 3610     |  4000
PackRotationsWithBuffer_Length9           | 3753     |  3625
PackRotationsWithBuffer_Length10          | 3607     |  4000
PackRotationsWithBuffer_Length9_optimized | 1946     |  3625
PackRotationsWithBuffer_Length9_inline    | 1453     |  3625

### Transform
PositionCompression bits:34
bounds 0 -> (200, 50, 200),
precision 0.05f

Method                                             | time(ms) | Bytes
---------------------------------------------------|----------|-------
WriteTranforms (compression)                       | 5189     | 10759
packTransforms (packed)                            | 5269     |  9125
packTransformsWithBuffer (packed)                  | 5234     |  9125 
oldWriteTranforms (Blittable)                      |  670     | 41759
oldWriteTranforms (Blittable+rotation compression) | 4012     | 29759
packTranformsWithBuffer (Optimized compression )   | 3431     |  9125


## Summary

- Blittable is by best for cpu
- Packed is best for bandwidth
- Packing and compression still have room to optimize for cpu