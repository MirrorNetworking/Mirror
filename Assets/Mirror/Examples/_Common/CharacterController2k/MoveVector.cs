using UnityEngine;

namespace Controller2k
{
    // A vector used by the OpenCharacterController.
    public struct MoveVector
    {
        // The move vector.
        // Note: This gets used up during the move loop, so will be zero by the end of the loop.
        public Vector3 moveVector;

        // Can the movement slide along obstacles?
        public bool canSlide;

        // Constructor.
        // newMoveVector: The move vector.
        // newCanSlide: Can the movement slide along obstacles?
        public MoveVector(Vector3 newMoveVector, bool newCanSlide = true)
        {
            moveVector = newMoveVector;
            canSlide = newCanSlide;
        }
    }
}