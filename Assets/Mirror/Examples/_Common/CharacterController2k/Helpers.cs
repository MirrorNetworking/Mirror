using UnityEngine;

namespace Controller2k
{
    public static class Helpers
    {
        // Get the top sphere's world position.
        public static Vector3 GetTopSphereWorldPosition(Vector3 position, Vector3 transformedCenter, float scaledRadius, float scaledHeight)
        {
            Vector3 sphereOffsetY = Vector3.up * (scaledHeight / 2.0f - scaledRadius);
            return position + transformedCenter + sphereOffsetY;
        }

        // added to simulate collision checks for CanSetHeight/Center
        // TODO can probably get rid of it and use the above function with proper parameters
        public static Vector3 GetTopSphereWorldPositionSimulated(Transform transform, Vector3 center, float scaledRadius, float height)
        {
            float scaledHeight = height * transform.lossyScale.y;
            Vector3 transformedCenter = transform.TransformVector(center);
            return GetTopSphereWorldPosition(transform.position, transformedCenter, scaledRadius, scaledHeight);
        }

        // Get the bottom sphere's world position.
        public static Vector3 GetBottomSphereWorldPosition(Vector3 position, Vector3 transformedCenter, float scaledRadius, float scaledHeight)
        {
            Vector3 sphereOffsetY = Vector3.up * (scaledHeight / 2.0f - scaledRadius);
            return position + transformedCenter - sphereOffsetY;
        }

        // added to simulate collision checks for CanSetHeight/Center
        // TODO can probably get rid of it and use the above function with proper parameters
        public static Vector3 GetBottomSphereWorldPositionSimulated(Transform transform, Vector3 center, float scaledRadius, float height)
        {
            float scaledHeight = height * transform.lossyScale.y;
            Vector3 transformedCenter = transform.TransformVector(center);
            return GetBottomSphereWorldPosition(transform.position, transformedCenter, scaledRadius, scaledHeight);
        }

        // Calculate a new center if the height changes and preserve the foot position.
        public static Vector3 CalculateCenterWithSameFootPosition(Vector3 center, float height, float newHeight, float skinWidth)
        {
            float localFootY = center.y - (height / 2.0f + skinWidth);
            float newCenterY = localFootY + (newHeight / 2.0f + skinWidth);
            return new Vector3(center.x, newCenterY, center.z);
        }

        // Is the movement vector almost zero (i.e. very small)?
        public static bool IsMoveVectorAlmostZero(Vector3 moveVector, float smallThreshold)
        {
            return Mathf.Abs(moveVector.x) <= smallThreshold &&
                   Mathf.Abs(moveVector.y) <= smallThreshold &&
                   Mathf.Abs(moveVector.z) <= smallThreshold;
        }
    }
}