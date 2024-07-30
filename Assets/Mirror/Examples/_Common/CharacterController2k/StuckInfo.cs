using UnityEngine;

namespace Controller2k
{
    // Stuck info and logic used by the OpenCharacterController.
    public class StuckInfo
    {
        // For keeping track of the character's position, to determine when the character gets stuck.
        Vector3? stuckPosition;

        // Count how long the character is in the same position.
        int stuckPositionCount;

        // If character's position does not change by more than this amount then we assume the character is stuck.
        const float k_StuckDistance = 0.001f;

        // If character collided this number of times during the movement loop then test if character is stuck by examining the position
        const int k_HitCountForStuck = 6;

        // Assume character is stuck if the position is the same for longer than this number of loop iterations
        const int k_MaxStuckPositionCount = 1;

        // Is the character stuck in the current move loop iteration?
        public bool isStuck;

        // Count the number of collisions during movement, to determine when the character gets stuck.
        public int hitCount;

        // Called when the move loop starts.
        public void OnMoveLoop()
        {
            hitCount = 0;
            stuckPositionCount = 0;
            stuckPosition = null;
            isStuck = false;
        }

        // Is the character stuck during the movement loop (e.g. bouncing between 2 or more colliders)?
        // characterPosition: The character's position.
        // currentMoveVector: Current move vector.
        // originalMoveVector: Original move vector.
        public bool UpdateStuck(Vector3 characterPosition, Vector3 currentMoveVector,
                                Vector3 originalMoveVector)
        {
            // First test
            if (!isStuck)
            {
                // From Quake2: "if velocity is against the original velocity, stop dead to avoid tiny occilations in sloping corners"
                if (currentMoveVector.sqrMagnitude.NotEqualToZero() &&
                    Vector3.Dot(currentMoveVector, originalMoveVector) <= 0.0f)
                {
                    isStuck = true;
                }
            }

            // Second test
            if (!isStuck)
            {
                // Test if collided and while position remains the same
                if (hitCount < k_HitCountForStuck)
                {
                    return false;
                }

                if (stuckPosition == null)
                {
                    stuckPosition = characterPosition;
                }
                else if (Vector3.Distance(stuckPosition.Value, characterPosition) <= k_StuckDistance)
                {
                    stuckPositionCount++;
                    if (stuckPositionCount > k_MaxStuckPositionCount)
                    {
                        isStuck = true;
                    }
                }
                else
                {
                    stuckPositionCount = 0;
                    stuckPosition = null;
                }
            }

            if (isStuck)
            {
                isStuck = false;
                hitCount = 0;
                stuckPositionCount = 0;
                stuckPosition = null;

                return true;
            }

            return false;
        }
    }
}