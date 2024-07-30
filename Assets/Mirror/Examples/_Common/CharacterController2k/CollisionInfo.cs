using UnityEngine;

namespace Controller2k
{
    // Collision info used by the OpenCharacterController and sent to the OnOpenCharacterControllerHit message.
    public struct CollisionInfo
    {
        // The collider that was hit by the controller.
        public readonly Collider collider;

        // The controller that hit the collider.
        public readonly CharacterController2k controller;

        // The game object that was hit by the controller.
        public readonly GameObject gameObject;

        // The direction the character Controller was moving in when the collision occured.
        public readonly Vector3 moveDirection;

        // How far the character has travelled until it hit the collider.
        public readonly float moveLength;

        // The normal of the surface we collided with in world space.
        public readonly Vector3 normal;

        // The impact point in world space.
        public readonly Vector3 point;

        // The rigidbody that was hit by the controller.
        public readonly Rigidbody rigidbody;

        // The transform that was hit by the controller.
        public readonly Transform transform;

        // Constructor
        // openCharacterController: The character controller that hit.
        // hitInfo: The hit info.
        // directionMoved: Direction moved when collision occured.
        // distanceMoved: How far the character has travelled until it hit the collider.
        public CollisionInfo(CharacterController2k openCharacterController,
                             RaycastHit hitInfo,
                             Vector3 directionMoved,
                             float distanceMoved)
        {
            collider = hitInfo.collider;
            controller = openCharacterController;
            gameObject = hitInfo.collider.gameObject;
            moveDirection = directionMoved;
            moveLength = distanceMoved;
            normal = hitInfo.normal;
            point = hitInfo.point;
            rigidbody = hitInfo.rigidbody;
            transform = hitInfo.transform;
        }
    }
}