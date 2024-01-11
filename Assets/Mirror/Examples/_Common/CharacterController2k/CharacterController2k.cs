// CharacterController2k is based on Unity's OpenCharacterController, modified by vis2k, all rights reserved.
//
// Collide & Slide based controller, instead of using Rigidbody.
// Read more: https://docs.nvidia.com/gameworks/content/gameworkslibrary/physx/guide/Manual/CharacterControllers.html
//
// -------------------------------------------------------------------------------------------------------------
// Original License from: https://github.com/Unity-Technologies/Standard-Assets-Characters:
// Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. Please review the license for details on these and other terms and conditions.
//
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Controller2k
{
    // sliding starts after a delay, and stops after a delay. we need an enum.
    public enum SlidingState : byte { NONE, STARTING, SLIDING, STOPPING };

    // <summary>
    // Open character controller. Handles the movement of a character, by using a capsule for movement and collision detection.
    // Note: The capsule is always upright. It ignores rotation.
    // </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterController2k : MonoBehaviour
    {
        // Fired on collision with colliders in the world
        public event Action<CollisionInfo> collision;

        [Header("Player Root")]
        [FormerlySerializedAs("m_PlayerRootTransform")]
        [Tooltip("The root bone in the avatar.")]
        public Transform playerRootTransform;

        [FormerlySerializedAs("m_RootTransformOffset")]
        [Tooltip("The root transform will be positioned at this offset.")]
        public Vector3 rootTransformOffset = Vector3.zero;

        [Header("Collision")]
        [FormerlySerializedAs("m_SlopeLimit")]
        [Tooltip("Limits the collider to only climb slopes that are less steep (in degrees) than the indicated value.")]
        public float slopeLimit = 45.0f;

        [FormerlySerializedAs("m_StepOffset")]
        [Tooltip("The character will step up a stair only if it is closer to the ground than the indicated value. " +
                 "This should not be greater than the Character Controller’s height or it will generate an error. " +
                 "Generally this should be kept as small as possible.")]
        public float stepOffset = 0.3f;

        [FormerlySerializedAs("m_SkinWidth")]
        [Tooltip("Two colliders can penetrate each other as deep as their Skin Width. Larger Skin Widths reduce jitter. " +
                 "Low Skin Width can cause the character to get stuck. A good setting is to make this value 10% of the Radius.")]
        public float skinWidth = 0.08f;

        [FormerlySerializedAs("m_GroundedTestDistance")]
        [Tooltip("Distance to test beneath the character when doing the grounded test. Increase if controller.isGrounded doesn't give the correct results or switches between true/false a lot.")]
        public float groundedTestDistance = 0.002f; // 0.001f isn't enough for big BoxColliders like uSurvival's Floor, even though it would work for MeshColliders.

        [FormerlySerializedAs("m_Center")]
        [Tooltip("This will offset the Capsule Collider in world space, and won’t affect how the Character pivots. " +
                 "Ideally, x and z should be zero to avoid rotating into another collider.")]
        public Vector3 center;

        [FormerlySerializedAs("m_Radius")]
        [Tooltip("Length of the Capsule Collider’s radius. This is essentially the width of the collider.")]
        public float radius = 0.5f;

        [FormerlySerializedAs("m_Height")]
        [Tooltip("The Character’s Capsule Collider height. It should be at least double the radius.")]
        public float height = 2.0f;

        [FormerlySerializedAs("m_CollisionLayerMask")]
        [Tooltip("Layers to test against for collisions.")]
        public LayerMask collisionLayerMask = ~0; // ~0 sets it to Everything

        [FormerlySerializedAs("m_IsLocalHuman")]
        [Tooltip("Is the character controlled by a local human? If true then more calculations are done for more accurate movement.")]
        public bool isLocalHuman = true;

        [FormerlySerializedAs("m_SlideAlongCeiling")]
        [Tooltip("Can character slide vertically when touching the ceiling? (For example, if ceiling is sloped.)")]
        public bool slideAlongCeiling = true;

        [FormerlySerializedAs("m_SlowAgainstWalls")]
        [Tooltip("Should the character slow down against walls?")]
        public bool slowAgainstWalls = false;

        [FormerlySerializedAs("m_MinSlowAgainstWallsAngle")]
        [Range(0.0f, 90.0f), Tooltip("The minimal angle from which the character will start slowing down on walls.")]
        public float minSlowAgainstWallsAngle = 10.0f;

        [FormerlySerializedAs("m_TriggerQuery")]
        [Tooltip("The desired interaction that cast calls should make against triggers")]
        public QueryTriggerInteraction triggerQuery = QueryTriggerInteraction.Ignore;

        [Header("Sliding")]
        [FormerlySerializedAs("m_SlideDownSlopes")]
        [Tooltip("Should the character slide down slopes when their angle is more than the slope limit?")]
        public bool slideDownSlopes = true;

        [FormerlySerializedAs("m_SlideMaxSpeed")]
        [Tooltip("The maximum speed that the character can slide downwards")]
        public float slideMaxSpeed = 10.0f;

        [FormerlySerializedAs("m_SlideGravityScale")]
        [Tooltip("Gravity multiplier to apply when sliding down slopes.")]
        public float slideGravityMultiplier = 1.0f;

        [FormerlySerializedAs("m_SlideStartTime")]
        [Tooltip("The time (in seconds) after initiating a slide classified as a slide start. Used to disable jumping.")]
        public float slideStartDelay = 0.1f;

        [Tooltip("Slight delay (in seconds) before we stop sliding down slopes. To handle cases where sliding test fails for a few frames.")]
        public float slideStopDelay = 0.1f;

        // Max slope limit.
        const float k_MaxSlopeLimit = 90.0f;

        // Max slope angle on which character can slide down automatically.
        const float k_MaxSlideAngle = 90.0f;

        // Distance to test for ground when sliding down slopes.
        const float k_SlideDownSlopeTestDistance = 1.0f;

        // Distance to push away from slopes when sliding down them.
        const float k_PushAwayFromSlopeDistance = 0.001f;

        // Minimum distance to use when checking ahead for steep slopes, when checking if it's safe to do the step offset.
        const float k_MinCheckSteepSlopeAheadDistance = 0.2f;

        // Min skin width.
        const float k_MinSkinWidth = 0.0001f;

        // The maximum move iterations. Mainly used as a fail safe to prevent an infinite loop.
        const int k_MaxMoveIterations = 20;

        // Stick to the ground if it is less than this distance from the character.
        const float k_MaxStickToGroundDownDistance = 1.0f;

        // Min distance to test for the ground when sticking to the ground.
        const float k_MinStickToGroundDownDistance = 0.01f;

        // Max colliders to use in the overlap methods.
        const int k_MaxOverlapColliders = 10;

        // Offset to use when moving to a collision point, to try to prevent overlapping the colliders
        const float k_CollisionOffset = 0.001f;

        // Minimum distance to move. This minimizes small penetrations and inaccurate casts (e.g. into the floor)
        const float k_MinMoveDistance = 0.0001f;

        // Minimum step offset height to move (if character has a step offset).
        const float k_MinStepOffsetHeight = k_MinMoveDistance;

        // Small value to test if the movement vector is small.
        const float k_SmallMoveVector = 1e-6f;

        // If angle between raycast and capsule/sphere cast normal is less than this then use the raycast normal, which is more accurate.
        const float k_MaxAngleToUseRaycastNormal = 5.0f;

        // Scale the capsule/sphere hit distance when doing the additional raycast to get a more accurate normal
        const float k_RaycastScaleDistance = 2.0f;

        // Slope check ahead is clamped by the distance moved multiplied by this scale.
        const float k_SlopeCheckDistanceMultiplier = 5.0f;

        // The capsule collider.
        CapsuleCollider m_CapsuleCollider;

        // The position at the start of the movement.
        Vector3 m_StartPosition;

        // Movement vectors used in the move loop.
        List<MoveVector> m_MoveVectors = new List<MoveVector>();

        // Next index in the moveVectors list.
        int m_NextMoveVectorIndex;

        // Surface normal of the last collision while moving down.
        Vector3? m_DownCollisionNormal;

        // Stuck info.
        StuckInfo m_StuckInfo = new StuckInfo();

        // The collision info when hitting colliders.
        Dictionary<Collider, CollisionInfo> m_CollisionInfoDictionary = new Dictionary<Collider, CollisionInfo>();

        // Collider array used for UnityEngine.Physics.OverlapCapsuleNonAlloc in GetPenetrationInfo
        readonly Collider[] m_PenetrationInfoColliders = new Collider[k_MaxOverlapColliders];

        // Velocity of the last movement. It's the new position minus the old position.
        // (should only be set internally, but needs to be readable for animations)
        public Vector3 velocity { get; private set; }

        // Default center of the capsule (e.g. for resetting it).
        Vector3 m_DefaultCenter;

        // Used to offset movement raycast when determining if a slope is travesable.
        float m_SlopeMovementOffset;

        // current sliding state
        // * NONE means we aren't sliding
        // * STARTING means we are standing on a slide, going go start soon
        // * SLIDING means we are sliding down right now
        // * STOPPING means we were standing on a slide, not anymore, just
        //   waiting a little longer so we don't immediately stop sliding when
        //   sliding over an tiny even surface
        public SlidingState slidingState { get; private set; }

        // We need to know how long a character has been sliding down a steep
        // slope in order to calculate sliding speed (it gets faster the longer
        // we have been sliding).
        // => remembering the start time is easier than adding += deltaTime
        //    everywhere.
        float slidingStartedTime;

        // we need to know when the physical sliding finished, so we can remain
        // in sliding state a little longer before we truly quit.
        // this is necessary so we don't immediately stop sliding if we slide
        // over a tiny flat surface.
        float slidingStoppedTime;

        // The capsule center with scaling and rotation applied.
        Vector3 transformedCenter { get { return transform.TransformVector(center); } }

        // The capsule height with the relevant scaling applied (e.g. if object scale is not 1,1,1)
        float scaledHeight { get { return height * transform.lossyScale.y; } }

        // Is the character on the ground? This is updated during Move or SetPosition.
        public bool isGrounded { get; private set; }

        // Collision flags from the last move.
        public CollisionFlags collisionFlags { get; private set; }

        // Default height of the capsule (e.g. for resetting it).
        public float defaultHeight { get; private set; }

        // The capsule radius with the relevant scaling applied (e.g. if object scale is not 1,1,1)
        public float scaledRadius
        {
            get
            {
                Vector3 scale = transform.lossyScale;
                float maxScale = Mathf.Max(Mathf.Max(scale.x, scale.y), scale.z);
                return radius * maxScale;
            }
        }

        // vis2k: add old character controller compatibility
        public Bounds bounds => m_CapsuleCollider.bounds;

        // Initialise the capsule and rigidbody, and set the root position.
        void Awake()
        {
            InitCapsuleColliderAndRigidbody();

            SetRootToOffset();

            m_SlopeMovementOffset =  stepOffset / Mathf.Tan(slopeLimit * Mathf.Deg2Rad);
        }

        // Update sliding down slopes, and changes to the capsule's height and center.
        void Update()
        {
            UpdateSlideDownSlopes();
        }

        // Set the root position.
        void LateUpdate()
        {
            SetRootToOffset();
        }

#if UNITY_EDITOR
        // Validate the capsule.
        void OnValidate()
        {
            Vector3 position = transform.position;
            ValidateCapsule(false, ref position);
            transform.position = position;
            SetRootToOffset();
        }

        // Draws the debug Gizmos
        void OnDrawGizmosSelected()
        {
            // Foot position
            Gizmos.color = Color.cyan;
            Vector3 footPosition = GetFootWorldPosition(transform.position);
            Gizmos.DrawLine(footPosition + Vector3.left * scaledRadius,
                            footPosition + Vector3.right * scaledRadius);
            Gizmos.DrawLine(footPosition + Vector3.back * scaledRadius,
                            footPosition + Vector3.forward * scaledRadius);

            // Top of head
            Vector3 headPosition = transform.position + transformedCenter + Vector3.up * (scaledHeight / 2.0f + skinWidth);
            Gizmos.DrawLine(headPosition + Vector3.left * scaledRadius,
                            headPosition + Vector3.right * scaledRadius);
            Gizmos.DrawLine(headPosition + Vector3.back * scaledRadius,
                            headPosition + Vector3.forward * scaledRadius);

            // Center position
            Vector3 centerPosition = transform.position + transformedCenter;
            Gizmos.DrawLine(centerPosition + Vector3.left * scaledRadius,
                            centerPosition + Vector3.right * scaledRadius);
            Gizmos.DrawLine(centerPosition + Vector3.back * scaledRadius,
                            centerPosition + Vector3.forward * scaledRadius);
        }
#endif

        // Move the character. This function does not apply any gravity.
        //   moveVector: Move along this vector.
        // CollisionFlags is the summary of collisions that occurred during the Move.
        public CollisionFlags Move(Vector3 moveVector)
        {
            MoveInternal(moveVector, true);
            return collisionFlags;
        }

        // Set the position of the character.
        //   position: Position to set.
        //   updateGrounded: Update the grounded state? This uses a cast, so only set it to true if you need it.
        public void SetPosition(Vector3 position, bool updateGrounded)
        {
            transform.position = position;

            if (updateGrounded)
            {
                UpdateGrounded(CollisionFlags.None);
            }
        }

        // Compute the minimal translation required to separate the character from the collider.
        //   positionOffset: Position offset to add to the capsule collider's position.
        //   collider: The collider to test.
        //   colliderPosition: Position of the collider.
        //   colliderRotation: Rotation of the collider.
        //   direction: Direction along which the translation required to separate the colliders apart is minimal.
        //   distance: The distance along direction that is required to separate the colliders apart.
        //   includeSkinWidth: Include the skin width in the test?
        //   currentPosition: Position of the character
        // True if found penetration.
        bool ComputePenetration(Vector3 positionOffset,
                                Collider collider, Vector3 colliderPosition, Quaternion colliderRotation,
                                out Vector3 direction, out float distance,
                                bool includeSkinWidth, Vector3 currentPosition)
        {
            if (collider == m_CapsuleCollider)
            {
                // Ignore self
                direction = Vector3.one;
                distance = 0;
                return false;
            }

            if (includeSkinWidth)
            {
                m_CapsuleCollider.radius = radius + skinWidth;
                m_CapsuleCollider.height = height + (skinWidth * 2.0f);
            }

            // Note: Physics.ComputePenetration does not always return values when the colliders overlap.
            bool result = Physics.ComputePenetration(m_CapsuleCollider,
                                                     currentPosition + positionOffset,
                                                     Quaternion.identity,
                                                     collider, colliderPosition, colliderRotation,
                                                     out direction, out distance);
            if (includeSkinWidth)
            {
                m_CapsuleCollider.radius = radius;
                m_CapsuleCollider.height = height;
            }

            return result;
        }

        // Check for collision below the character, using a ray or sphere cast.
        //   distance: Distance to check.
        //   hitInfo: Get the hit info.
        //   offsetPosition: Position offset. If we want to do a cast relative to the character's current position.
        //   useSphereCast: Use a sphere cast? If false then use a ray cast.
        //   useSecondSphereCast: The second cast includes the skin width. Ideally only needed for human controlled player, for more accuracy.
        //   adjustPositionSlightly: Adjust position slightly up, in case it's already inside an obstacle.
        //   currentPosition: Position of the character
        // True if collision occurred.
        public bool CheckCollisionBelow(float distance, out RaycastHit hitInfo, Vector3 currentPosition,
                                        Vector3 offsetPosition,
                                        bool useSphereCast = false,
                                        bool useSecondSphereCast = false,
                                        bool adjustPositionSlightly = false)
        {
            bool didCollide = false;
            float extraDistance = adjustPositionSlightly ? k_CollisionOffset : 0.0f;
            if (!useSphereCast)
            {
#if UNITY_EDITOR
                Vector3 start = GetFootWorldPosition(currentPosition) + offsetPosition + Vector3.up * extraDistance;
                Debug.DrawLine(start, start + Vector3.down * (distance + extraDistance), Color.red);
#endif
                if (Physics.Raycast(GetFootWorldPosition(currentPosition) + offsetPosition + Vector3.up * extraDistance,
                                    Vector3.down,
                                    out hitInfo,
                                    distance + extraDistance,
                                    collisionLayerMask,
                                    triggerQuery))
                {
                    didCollide = true;
                    hitInfo.distance = Mathf.Max(0.0f, hitInfo.distance - extraDistance);
                }
            }
            else
            {
#if UNITY_EDITOR
                Debug.DrawRay(currentPosition, Vector3.down, Color.red); // Center

                Debug.DrawRay(currentPosition +  new Vector3(scaledRadius, 0.0f), Vector3.down, Color.blue);
                Debug.DrawRay(currentPosition +  new Vector3(-scaledRadius, 0.0f), Vector3.down, Color.blue);
                Debug.DrawRay(currentPosition +  new Vector3(0.0f, 0.0f, scaledRadius), Vector3.down, Color.blue);
                Debug.DrawRay(currentPosition +  new Vector3(0.0f, 0.0f, -scaledRadius), Vector3.down, Color.blue);
#endif
                if (SmallSphereCast(Vector3.down,
                                    skinWidth + distance,
                                    out hitInfo,
                                    offsetPosition,
                                    true, currentPosition))
                {
                    didCollide = true;
                    hitInfo.distance = Mathf.Max(0.0f, hitInfo.distance - skinWidth);
                }

                if (!didCollide && useSecondSphereCast)
                {
                    if (BigSphereCast(Vector3.down,
                                      distance + extraDistance, currentPosition,
                                      out hitInfo,
                                      offsetPosition + Vector3.up * extraDistance,
                                      true))
                    {
                        didCollide = true;
                        hitInfo.distance = Mathf.Max(0.0f, hitInfo.distance - extraDistance);
                    }
                }
            }

            return didCollide;
        }

        // Try to set the capsule's height and center. Originally, this would
        // keep trying every Update until it's safe to resize. this required a
        // LOT of magic. It now either resizes immediately or it returns false
        // if there is no space.
        //   newHeight: The new height.
        //   newCenter: The new center.
        //   checkForPenetration: Check for collision, and then de-penetrate if there's collision?
        //   updateGrounded: Update the grounded state? This uses a cast, so only set it to true if you need it.
        public bool TrySetHeightAndCenter(float newHeight, Vector3 newCenter,
                                          bool checkForPenetration,
                                          bool updateGrounded)
        {
            // only resize if we will succeed. otherwise don't.
            if (checkForPenetration &&
                !CanSetHeightAndCenter(newHeight, newCenter))
                return false;

            float oldHeight = height;
            Vector3 oldCenter = center;
            Vector3 oldPosition = transform.position;
            Vector3 virtualPosition = oldPosition;

            // set center, set height
            // if either one fails, we don't return immediately.
            // we want to do the penetration test no matter what,
            // and restore data no matter what if it fails.
            bool result = TrySetCenter(newCenter, false, false) &&
                          TrySetHeight(newHeight, false, false, false);

            if (checkForPenetration)
            {
                if (Depenetrate(ref virtualPosition))
                {
                    // Inside colliders?
                    if (CheckCapsule(virtualPosition))
                    {
                        // Restore data
                        // NOTE: even though we simulate the resize first, we
                        // might still end up inside a collider after resizing
                        // because the simulation isn't 100% precise. so we
                        // sometimes still get here when it 'almost' succeeeds
                        height = oldHeight;
                        center = oldCenter;
                        transform.position = oldPosition;
                        ValidateCapsule(true, ref virtualPosition);

                        // return false later
                        result = false;
                    }
                }
            }

            if (updateGrounded)
            {
                UpdateGrounded(CollisionFlags.None);
            }

            transform.position = virtualPosition;
            return result;
        }

        // Reset the capsule's height and center to the default values.
        //   checkForPenetration: Check for collision, and then de-penetrate if there's collision?
        //   updateGrounded: Update the grounded state? This uses a cast, so only set it to true if you need it.
        public bool TryResetHeightAndCenter(bool checkForPenetration, bool updateGrounded)
        {
            return TrySetHeightAndCenter(defaultHeight, m_DefaultCenter, checkForPenetration, updateGrounded);
        }

        // Try to set the capsule's center (local). Originally, this would
        // keep trying every Update until it's safe to resize. this required a
        // LOT of magic. It now either resizes immediately or it returns false
        // if there is no space.
        //   newCenter: The new center.
        //   checkForPenetration: Check for collision, and then de-penetrate if there's collision?
        //   updateGrounded: Update the grounded state? This uses a cast, so only set it to true if you need it.
        public bool TrySetCenter(Vector3 newCenter, bool checkForPenetration, bool updateGrounded)
        {
            // only resize if we will succeed. otherwise don't.
            if (checkForPenetration &&
                !CanSetCenter(newCenter))
                return false;

            Vector3 oldCenter = center;
            Vector3 oldPosition = transform.position;
            Vector3 virtualPosition = oldPosition;

            center = newCenter;
            ValidateCapsule(true, ref virtualPosition);

            bool result = true;
            if (checkForPenetration)
            {
                if (Depenetrate(ref virtualPosition))
                {
                    // Inside colliders?
                    if (CheckCapsule(virtualPosition))
                    {
                        // Restore data
                        // NOTE: even though we simulate the resize first, we
                        // might still end up inside a collider after resizing
                        // because the simulation isn't 100% precise. so we
                        // sometimes still get here when it 'almost' succeeeds
                        center = oldCenter;
                        transform.position = oldPosition;
                        ValidateCapsule(true, ref virtualPosition);

                        // return false later
                        result = false;
                    }
                }
            }

            if (updateGrounded)
            {
                UpdateGrounded(CollisionFlags.None);
            }

            transform.position = virtualPosition;
            return result;
        }

        // Reset the capsule's center to the default value.
        //   checkForPenetration: Check for collision, and then de-penetrate if there's collision?
        //   updateGrounded: Update the grounded state? This uses a cast, so only set it to true if you need it.
        public bool TryResetCenter(bool checkForPenetration, bool updateGrounded)
        {
            return TrySetCenter(m_DefaultCenter, checkForPenetration, updateGrounded);
        }

        // Validate the capsule's height. (It must be at least double the radius size.)
        public float ValidateHeight(float newHeight)
        {
            return Mathf.Clamp(newHeight, radius * 2.0f, float.MaxValue);
        }

        // Set the capsule's height (local). Minimum limit is double the capsule radius size.
        // Call CanSetHeight if you want to test if height can change, e.g. when changing from crouch to stand.
        //   newHeight: The new height.
        //   preserveFootPosition: Adjust the capsule's center to preserve the foot position?
        //   checkForPenetration: Check for collision, and then de-penetrate if there's collision?
        //   updateGrounded: Update the grounded state? This uses a cast, so only set it to true if you need it.
        public bool TrySetHeight(float newHeight, bool preserveFootPosition,
                                 bool checkForPenetration,
                                 bool updateGrounded)
        {
            // only resize if we will succeed. otherwise don't.
            if (checkForPenetration &&
                !CanSetHeight(newHeight, preserveFootPosition))
                return false;

            // vis2k fix:
            // IMPORTANT: adjust height BEFORE ever calculating the center.
            //            previously it was adjusted AFTER calculating the center.
            //            so the center would NOT EXACTLY be the center anymore
            //            if the height was adjusted.
            //            => causing all future center calculations to be wrong.
            //            => causing center.y to increase every time
            //            => causing the character to float in the air over time
            //            see also: https://github.com/vis2k/uMMORPG/issues/36
            newHeight = ValidateHeight(newHeight);

            Vector3 virtualPosition = transform.position;
            bool changeCenter = preserveFootPosition;
            Vector3 newCenter = changeCenter ? Helpers.CalculateCenterWithSameFootPosition(center, height, newHeight, skinWidth) : center;
            if (Mathf.Approximately(height, newHeight))
            {
                // Height remains the same. only set new center, which may have
                // changed because of preserveFootPosition
                return TrySetCenter(newCenter, checkForPenetration, updateGrounded);
            }

            float oldHeight = height;
            Vector3 oldCenter = center;
            Vector3 oldPosition = transform.position;

            if (changeCenter)
            {
                center = newCenter;
            }

            height = newHeight;
            ValidateCapsule(true, ref virtualPosition);

            bool result = true;
            if (checkForPenetration)
            {
                if (Depenetrate(ref virtualPosition))
                {
                    // Inside colliders?
                    if (CheckCapsule(virtualPosition))
                    {
                        // Restore data
                        // NOTE: even though we simulate the resize first, we
                        // might still end up inside a collider after resizing
                        // because the simulation isn't 100% precise. so we
                        // sometimes still get here when it 'almost' succeeeds
                        height = oldHeight;
                        if (changeCenter)
                        {
                            center = oldCenter;
                        }
                        transform.position = oldPosition;
                        ValidateCapsule(true, ref virtualPosition);

                        // return false later
                        result = false;
                    }
                }
            }

            if (updateGrounded)
            {
                UpdateGrounded(CollisionFlags.None);
            }

            transform.position = virtualPosition;
            return result;
        }

        // vis2k: add missing CanSetHeight function & helper functions
        // Collider array used for UnityEngine.Physics.OverlapCapsuleNonAlloc in GetPenetrationInfo
        readonly Collider[] m_OverlapCapsuleColliders = new Collider[k_MaxOverlapColliders];
        public bool CanSetHeight(float newHeight, bool preserveFootPosition)
        {
            // vis2k fix:
            // IMPORTANT: adjust height BEFORE ever calculating the center.
            //            previously it was adjusted AFTER calculating the center.
            //            so the center would NOT EXACTLY be the center anymore
            //            if the height was adjusted.
            //            => causing all future center calculations to be wrong.
            //            => causing center.y to increase every time
            //            => causing the character to float in the air over time
            //            see also: https://github.com/vis2k/uMMORPG/issues/36
            newHeight = ValidateHeight(newHeight);

            // calculate the new capsule center & height
            bool changeCenter = preserveFootPosition;
            Vector3 newCenter = changeCenter ? Helpers.CalculateCenterWithSameFootPosition(center, height, newHeight, skinWidth) : center;
            if (Mathf.Approximately(height, newHeight))
            {
                // Height remains the same
                return true;
            }

            return CanSetHeightAndCenter(newHeight, newCenter);
        }

        // vis2k: add missing CanSetCenter function
        public bool CanSetCenter(Vector3 newCenter)
        {
            return CanSetHeightAndCenter(height, newCenter);
        }

        // vis2k: add missing CanSetHeightAndCenter function
        public bool CanSetHeightAndCenter(float newHeight, Vector3 newCenter)
        {
            // vis2k fix:
            // IMPORTANT: adjust height BEFORE ever calculating the center.
            //            previously it was adjusted AFTER calculating the center.
            //            so the center would NOT EXACTLY be the center anymore
            //            if the height was adjusted.
            //            => causing all future center calculations to be wrong.
            //            => causing center.y to increase every time
            //            => causing the character to float in the air over time
            //            see also: https://github.com/vis2k/uMMORPG/issues/36
            newHeight = ValidateHeight(newHeight);

            // debug draw
            Debug.DrawLine(
                Helpers.GetTopSphereWorldPositionSimulated(transform, newCenter, scaledRadius, newHeight),
                Helpers.GetBottomSphereWorldPositionSimulated(transform, newCenter, scaledRadius, newHeight),
                Color.yellow,
                3f
            );

            // check the overlap capsule
            int hits = Physics.OverlapCapsuleNonAlloc(
                Helpers.GetTopSphereWorldPositionSimulated(transform, newCenter, scaledRadius, newHeight),
                Helpers.GetBottomSphereWorldPositionSimulated(transform, newCenter, scaledRadius, newHeight),
                radius,
                m_OverlapCapsuleColliders,
                collisionLayerMask,
                triggerQuery);

            for (int i = 0; i < hits; ++i)
            {
                // a collider that is not self?
                if (m_OverlapCapsuleColliders[i] != m_CapsuleCollider)
                {
                    return false;
                }
            }

            // no overlaps
            return true;
        }

        // Reset the capsule's height to the default value.
        //   preserveFootPosition: Adjust the capsule's center to preserve the foot position?
        //   checkForPenetration: Check for collision, and then de-penetrate if there's collision?
        //   updateGrounded: Update the grounded state? This uses a cast, so only set it to true if you need it.
        // Returns the reset height.
        public bool TryResetHeight(bool preserveFootPosition, bool checkForPenetration, bool updateGrounded)
        {
            return TrySetHeight(defaultHeight, preserveFootPosition, checkForPenetration, updateGrounded);
        }

        // Get the foot world position.
        public Vector3 GetFootWorldPosition()
        {
            return GetFootWorldPosition(transform.position);
        }

        // Get the foot world position.
        Vector3 GetFootWorldPosition(Vector3 position)
        {
            return position + transformedCenter + (Vector3.down * (scaledHeight / 2.0f + skinWidth));
        }

        // Initialize the capsule collider and the rigidbody
        void InitCapsuleColliderAndRigidbody()
        {
            GameObject go = transform.gameObject;
            m_CapsuleCollider = go.GetComponent<CapsuleCollider>();

            // Copy settings to the capsule collider
            m_CapsuleCollider.center = center;
            m_CapsuleCollider.radius = radius;
            m_CapsuleCollider.height = height;

            // Ensure that the rigidbody is kinematic and does not use gravity
            Rigidbody rigidbody = go.GetComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            defaultHeight = height;
            m_DefaultCenter = center;
        }

        // Call this when the capsule's values change.
        //      updateCapsuleCollider: Update the capsule collider's values (e.g. center, height, radius)?
        //      currentPosition: position of the character
        //      checkForPenetration: Check for collision, and then de-penetrate if there's collision?
        //      updateGrounded: Update the grounded state? This uses a cast, so only set it to true if you need it.
        void ValidateCapsule(bool updateCapsuleCollider,
                             ref Vector3 currentPosition,
                             bool checkForPenetration = false,
                             bool updateGrounded = false)
        {
            slopeLimit = Mathf.Clamp(slopeLimit, 0.0f, k_MaxSlopeLimit);
            skinWidth = Mathf.Clamp(skinWidth, k_MinSkinWidth, float.MaxValue);
            float oldHeight = height;
            height = ValidateHeight(height);

            if (m_CapsuleCollider != null)
            {
                if (updateCapsuleCollider)
                {
                    // Copy settings to the capsule collider
                    m_CapsuleCollider.center = center;
                    m_CapsuleCollider.radius = radius;
                    m_CapsuleCollider.height = height;
                }
                else if (!Mathf.Approximately(height, oldHeight))
                {
                    // Height changed
                    m_CapsuleCollider.height = height;
                }
            }

            if (checkForPenetration)
            {
                Depenetrate(ref currentPosition);
            }

            if (updateGrounded)
            {
                UpdateGrounded(CollisionFlags.None);
            }
        }

        // Moves the characters.
        //      moveVector: Move vector
        //      slideWhenMovingDown: Slide against obstacles when moving down? (e.g. we don't want to slide when applying gravity while the character is grounded)
        //      forceTryStickToGround: Force try to stick to ground? Only used if character is grounded before moving.
        //      doNotStepOffset: Do not try to perform the step offset?
        void MoveInternal(Vector3 moveVector,
                          bool slideWhenMovingDown,
                          bool forceTryStickToGround = false,
                          bool doNotStepOffset = false)
        {
            bool wasGrounded = isGrounded;
            Vector3 moveVectorNoY = new Vector3(moveVector.x, 0, moveVector.z);
            bool tryToStickToGround = wasGrounded && (forceTryStickToGround || (moveVector.y <= 0.0f && moveVectorNoY.sqrMagnitude.NotEqualToZero()));

            m_StartPosition = transform.position;

            collisionFlags = CollisionFlags.None;
            m_CollisionInfoDictionary.Clear();
            m_DownCollisionNormal = null;

            // Stop sliding down slopes when character jumps
            if (moveVector.y > 0.0f && slidingState != SlidingState.NONE)
            {
                // this hasn't really been tested. let's show a log message for
                // now.
                Debug.Log("CharacterController2k: a jump stopped sliding: " + slidingState);
                slidingState = SlidingState.NONE;
            }

            // Do the move loop
            MoveLoop(moveVector, tryToStickToGround, slideWhenMovingDown, doNotStepOffset);

            bool doDownCast = tryToStickToGround || moveVector.y <= 0.0f;
            UpdateGrounded(collisionFlags, doDownCast);

            // vis2k: fix velocity
            // set velocity, which is direction * speed. we don't have speed,
            // but we do have elapsed time
            velocity = (transform.position - m_StartPosition) / Time.deltaTime;

            BroadcastCollisionEvent();
        }

        // Send hit messages.
        void BroadcastCollisionEvent()
        {
            if (collision == null || m_CollisionInfoDictionary == null || m_CollisionInfoDictionary.Count <= 0)
            {
                return;
            }

            foreach (KeyValuePair<Collider, CollisionInfo> kvp in m_CollisionInfoDictionary)
            {
                collision(kvp.Value);
            }
        }

        // Determine if the character is grounded.
        //      movedCollisionFlags: Moved collision flags of the current move. Set to None if not moving.
        //      doDownCast: Do a down cast? We want to avoid this when the character is jumping upwards.
        void UpdateGrounded(CollisionFlags movedCollisionFlags, bool doDownCast = true)
        {
            if ((movedCollisionFlags & CollisionFlags.CollidedBelow) != 0)
            {
                isGrounded = true;
            }
            else if (doDownCast)
            {
                isGrounded = CheckCollisionBelow(groundedTestDistance,
                                                 out RaycastHit hitInfo,
                                                 transform.position,
                                                 Vector3.zero,
                                                 true,
                                                 isLocalHuman,
                                                 isLocalHuman);
            }
            else
            {
                isGrounded = false;
            }
        }

        // Movement loop. Keep moving until completely blocked by obstacles, or we reached the desired position/distance.
        //      moveVector: The move vector.
        //      tryToStickToGround: Try to stick to the ground?
        //      slideWhenMovingDown: Slide against obstacles when moving down? (e.g. we don't want to slide when applying gravity while the character is grounded)
        //      doNotStepOffset: Do not try to perform the step offset?
        void MoveLoop(Vector3 moveVector, bool tryToStickToGround, bool slideWhenMovingDown, bool doNotStepOffset)
        {
            m_MoveVectors.Clear();
            m_NextMoveVectorIndex = 0;

            // Split the move vector into horizontal and vertical components.
            SplitMoveVector(moveVector, slideWhenMovingDown, doNotStepOffset);
            MoveVector remainingMoveVector = m_MoveVectors[m_NextMoveVectorIndex];
            m_NextMoveVectorIndex++;

            bool didTryToStickToGround = false;
            m_StuckInfo.OnMoveLoop();
            Vector3 virtualPosition = transform.position;

            // The loop
            for (int i = 0; i < k_MaxMoveIterations; i++)
            {
                Vector3 refMoveVector = remainingMoveVector.moveVector;
                bool collided = MoveMajorStep(ref refMoveVector, remainingMoveVector.canSlide, didTryToStickToGround, ref virtualPosition);

                remainingMoveVector.moveVector = refMoveVector;

                // Character stuck?
                if (m_StuckInfo.UpdateStuck(virtualPosition, remainingMoveVector.moveVector, moveVector))
                {
                    // Stop current move loop vector
                    remainingMoveVector = new MoveVector(Vector3.zero);
                }
                else if (!isLocalHuman && collided)
                {
                    // Only slide once for non-human controlled characters
                    remainingMoveVector.canSlide = false;
                }

                // Not collided OR vector used up (i.e. vector is zero)?
                if (!collided || remainingMoveVector.moveVector.sqrMagnitude.IsEqualToZero())
                {
                    // Are there remaining movement vectors?
                    if (m_NextMoveVectorIndex < m_MoveVectors.Count)
                    {
                        remainingMoveVector = m_MoveVectors[m_NextMoveVectorIndex];
                        m_NextMoveVectorIndex++;
                    }
                    else
                    {
                        if (!tryToStickToGround || didTryToStickToGround)
                        {
                            break;
                        }

                        // Try to stick to the ground
                        didTryToStickToGround = true;
                        if (!CanStickToGround(moveVector, out remainingMoveVector))
                        {
                            break;
                        }
                    }
                }

#if UNITY_EDITOR
                if (i == k_MaxMoveIterations - 1)
                {
                    Debug.Log(name + " reached MaxMoveInterations(" + k_MaxMoveIterations + "): remainingVector=" + remainingMoveVector + " moveVector=" + moveVector + " hitCount=" + m_StuckInfo.hitCount);
                }
#endif
            }

            transform.position = virtualPosition;
        }

        // A single movement major step. Returns true when there is collision.
        //      moveVector: The move vector.
        //      canSlide: Can slide against obstacles?
        //      tryGrounding: Try grounding the player?
        //      currentPosition: position of the character
        bool MoveMajorStep(ref Vector3 moveVector, bool canSlide, bool tryGrounding, ref Vector3 currentPosition)
        {
            Vector3 direction = moveVector.normalized;
            float distance = moveVector.magnitude;
            RaycastHit bigRadiusHitInfo;
            RaycastHit smallRadiusHitInfo;
            bool smallRadiusHit;
            bool bigRadiusHit;

            if (!CapsuleCast(direction, distance, currentPosition,
                             out smallRadiusHit, out bigRadiusHit,
                             out smallRadiusHitInfo, out bigRadiusHitInfo,
                             Vector3.zero))
            {
                // No collision, so move to the position
                MovePosition(moveVector, null, null, ref currentPosition);

                // Check for penetration
                float penetrationDistance;
                Vector3 penetrationDirection;
                if (GetPenetrationInfo(out penetrationDistance, out penetrationDirection, currentPosition))
                {
                    // Push away from obstacles
                    MovePosition(penetrationDirection * penetrationDistance, null, null, ref currentPosition);
                }

                // Stop current move loop vector
                moveVector = Vector3.zero;

                return false;
            }

            // Did the big radius not hit an obstacle?
            if (!bigRadiusHit)
            {
                // The small radius hit an obstacle, so character is inside an obstacle
                MoveAwayFromObstacle(ref moveVector, ref smallRadiusHitInfo,
                                     direction, distance,
                                     canSlide,
                                     tryGrounding,
                                     true, ref currentPosition);

                return true;
            }

            // Use the nearest collision point (e.g. to handle cases where 2 or more colliders' edges meet)
            if (smallRadiusHit && smallRadiusHitInfo.distance < bigRadiusHitInfo.distance)
            {
                MoveAwayFromObstacle(ref moveVector, ref smallRadiusHitInfo,
                                     direction, distance,
                                     canSlide,
                                     tryGrounding,
                                     true, ref currentPosition);
                return true;
            }

            MoveAwayFromObstacle(ref moveVector, ref bigRadiusHitInfo,
                                 direction, distance,
                                 canSlide,
                                 tryGrounding,
                                 false, ref currentPosition);

            return true;
        }

        // Can the character perform a step offset?
        //      moveVector: Horizontal movement vector.
        bool CanStepOffset(Vector3 moveVector)
        {
            float moveVectorMagnitude = moveVector.magnitude;
            Vector3 position = transform.position;
            RaycastHit hitInfo;

            // Only step up if there's an obstacle at the character's feet (e.g. do not step when only character's head collides)
            if (!SmallSphereCast(moveVector, moveVectorMagnitude, out hitInfo, Vector3.zero, true, position) &&
                !BigSphereCast(moveVector, moveVectorMagnitude, position, out hitInfo, Vector3.zero, true))
            {
                return false;
            }

            float upDistance = Mathf.Max(stepOffset, k_MinStepOffsetHeight);

            // We only step over obstacles if we can partially fit on it (i.e. fit the capsule's radius)
            Vector3 horizontal = moveVector * scaledRadius;
            float horizontalSize = horizontal.magnitude;
            horizontal.Normalize();

            // Any obstacles ahead (after we moved up)?
            Vector3 up = Vector3.up * upDistance;
            if (SmallCapsuleCast(horizontal, skinWidth + horizontalSize, out hitInfo, up, position) ||
                BigCapsuleCast(horizontal, horizontalSize, out hitInfo, up, position))
            {
                return false;
            }

            return !CheckSteepSlopeAhead(moveVector);
        }

        // Returns true if there's a steep slope ahead.
        //      moveVector: The movement vector.
        //      alsoCheckForStepOffset: Do a second test where the step offset will move the player to?
        bool CheckSteepSlopeAhead(Vector3 moveVector, bool alsoCheckForStepOffset = true)
        {
            Vector3 direction = moveVector.normalized;
            float distance = moveVector.magnitude;

            if (CheckSteepSlopAhead(direction, distance, Vector3.zero))
            {
                return true;
            }

            // Only need to do the second test for human controlled character
            if (!alsoCheckForStepOffset || !isLocalHuman)
            {
                return false;
            }

            // Check above where the step offset will move the player to
            return CheckSteepSlopAhead(direction,
                                       Mathf.Max(distance, k_MinCheckSteepSlopeAheadDistance),
                                       Vector3.up * stepOffset);
        }

        // Returns true if there's a steep slope ahead.
        bool CheckSteepSlopAhead(Vector3 direction, float distance, Vector3 offsetPosition)
        {
            RaycastHit bigRadiusHitInfo;
            RaycastHit smallRadiusHitInfo;
            bool smallRadiusHit;
            bool bigRadiusHit;

            if (!CapsuleCast(direction, distance, transform.position,
                             out smallRadiusHit, out bigRadiusHit,
                             out smallRadiusHitInfo, out bigRadiusHitInfo,
                             offsetPosition))
            {
                // No collision
                return false;
            }

            RaycastHit hitInfoCapsule = (!bigRadiusHit || (smallRadiusHit && smallRadiusHitInfo.distance < bigRadiusHitInfo.distance))
                                        ? smallRadiusHitInfo
                                        : bigRadiusHitInfo;

            RaycastHit hitInfoRay;
            Vector3 rayOrigin = transform.position + transformedCenter + offsetPosition;

            float offset = Mathf.Clamp(m_SlopeMovementOffset, 0.0f, distance * k_SlopeCheckDistanceMultiplier);
            Vector3 rayDirection = (hitInfoCapsule.point + direction * offset) - rayOrigin;

            // Raycast returns a more accurate normal than SphereCast/CapsuleCast
            if (Physics.Raycast(rayOrigin,
                                rayDirection,
                                out hitInfoRay,
                                rayDirection.magnitude * k_RaycastScaleDistance,
                                collisionLayerMask,
                                triggerQuery) &&
                hitInfoRay.collider == hitInfoCapsule.collider)
            {
                hitInfoCapsule = hitInfoRay;
            }
            else
            {
                return false;
            }

            float slopeAngle = Vector3.Angle(Vector3.up, hitInfoCapsule.normal);
            bool slopeIsSteep = slopeAngle > slopeLimit &&
                                slopeAngle < k_MaxSlopeLimit &&
                                Vector3.Dot(direction, hitInfoCapsule.normal) < 0.0f;

            return slopeIsSteep;
        }

        // Split the move vector into horizontal and vertical components. The results are added to the moveVectors list.
        //      moveVector: The move vector.
        //      slideWhenMovingDown: Slide against obstacles when moving down? (e.g. we don't want to slide when applying gravity while the character is grounded)
        //      doNotStepOffset: Do not try to perform the step offset?
        void SplitMoveVector(Vector3 moveVector, bool slideWhenMovingDown, bool doNotStepOffset)
        {
            Vector3 horizontal = new Vector3(moveVector.x, 0.0f, moveVector.z);
            Vector3 vertical = new Vector3(0.0f, moveVector.y, 0.0f);
            bool horizontalIsAlmostZero = Helpers.IsMoveVectorAlmostZero(horizontal, k_SmallMoveVector);
            float tempStepOffset = stepOffset;
            bool doStepOffset = isGrounded &&
                                !doNotStepOffset &&
                                !Mathf.Approximately(tempStepOffset, 0.0f) &&
                                !horizontalIsAlmostZero;

            // Note: Vector is split in this order: up, horizontal, down

            if (vertical.y > 0.0f)
            {
                // Up
                if (horizontal.x.NotEqualToZero() || horizontal.z.NotEqualToZero())
                {
                    // Move up then horizontal
                    AddMoveVector(vertical, slideAlongCeiling);
                    AddMoveVector(horizontal);
                }
                else
                {
                    // Move up
                    AddMoveVector(vertical, slideAlongCeiling);
                }
            }
            else if (vertical.y < 0.0f)
            {
                // Down
                if (horizontal.x.NotEqualToZero() || horizontal.z.NotEqualToZero())
                {
                    if (doStepOffset && CanStepOffset(horizontal))
                    {
                        // Move up, horizontal then down
                        AddMoveVector(Vector3.up * tempStepOffset, false);
                        AddMoveVector(horizontal);
                        if (slideWhenMovingDown)
                        {
                            AddMoveVector(vertical);
                            AddMoveVector(Vector3.down * tempStepOffset);
                        }
                        else
                        {
                            AddMoveVector(vertical + Vector3.down * tempStepOffset);
                        }
                    }
                    else
                    {
                        // Move horizontal then down
                        AddMoveVector(horizontal);
                        AddMoveVector(vertical, slideWhenMovingDown);
                    }
                }
                else
                {
                    // Move down
                    AddMoveVector(vertical, slideWhenMovingDown);
                }
            }
            else
            {
                // Horizontal
                if (doStepOffset && CanStepOffset(horizontal))
                {
                    // Move up, horizontal then down
                    AddMoveVector(Vector3.up * tempStepOffset, false);
                    AddMoveVector(horizontal);
                    AddMoveVector(Vector3.down * tempStepOffset);
                }
                else
                {
                    // Move horizontal
                    AddMoveVector(horizontal);
                }
            }
        }

        // Add the movement vector to the moveVectors list.
        //      moveVector: Move vector to add.
        //      canSlide: Can the movement slide along obstacles?
        void AddMoveVector(Vector3 moveVector, bool canSlide = true)
        {
            m_MoveVectors.Add(new MoveVector(moveVector, canSlide));
        }

        // Test if character can stick to the ground, and set the down vector if so.
        //      moveVector: The original movement vector.
        //      getDownVector: Get the down vector.
        bool CanStickToGround(Vector3 moveVector, out MoveVector getDownVector)
        {
            Vector3 moveVectorNoY = new Vector3(moveVector.x, 0.0f, moveVector.z);
            float downDistance = Mathf.Max(moveVectorNoY.magnitude, k_MinStickToGroundDownDistance);
            if (moveVector.y < 0.0f)
            {
                downDistance = Mathf.Max(downDistance, Mathf.Abs(moveVector.y));
            }

            if (downDistance <= k_MaxStickToGroundDownDistance)
            {
                getDownVector = new MoveVector(Vector3.down * downDistance, false);
                return true;
            }

            getDownVector = new MoveVector(Vector3.zero);
            return false;
        }

        // Do two capsule casts. One excluding the capsule's skin width and one including the skin width.
        //      direction: Direction to cast
        //      distance: Distance to cast
        //      currentPosition: position of the character
        //      smallRadiusHit: Did hit, excluding the skin width?
        //      bigRadiusHit: Did hit, including the skin width?
        //      smallRadiusHitInfo: Hit info for cast excluding the skin width.
        //      bigRadiusHitInfo: Hit info for cast including the skin width.
        //      offsetPosition: Offset position, if we want to test somewhere relative to the capsule's position.
        bool CapsuleCast(Vector3 direction, float distance, Vector3 currentPosition,
                                 out bool smallRadiusHit, out bool bigRadiusHit,
                                 out RaycastHit smallRadiusHitInfo, out RaycastHit bigRadiusHitInfo,
                                 Vector3 offsetPosition)
        {
            // Exclude the skin width in the test
            smallRadiusHit = SmallCapsuleCast(direction, distance, out smallRadiusHitInfo, offsetPosition, currentPosition);

            // Include the skin width in the test
            bigRadiusHit = BigCapsuleCast(direction, distance, out bigRadiusHitInfo, offsetPosition, currentPosition);

            return smallRadiusHit || bigRadiusHit;
        }

        // Do a capsule cast, excluding the skin width.
        //      direction: Direction to cast.
        //      distance: Distance to cast.
        //      smallRadiusHitInfo: Hit info.
        //      offsetPosition: Offset position, if we want to test somewhere relative to the capsule's position.
        //      currentPosition: position of the character
        bool SmallCapsuleCast(Vector3 direction, float distance,
                              out RaycastHit smallRadiusHitInfo,
                              Vector3 offsetPosition, Vector3 currentPosition)
        {
            // Cast further than the distance we need, to try to take into account small edge cases (e.g. Casts fail
            // when moving almost parallel to an obstacle for small distances).
            float extraDistance = scaledRadius;

            if (Physics.CapsuleCast(Helpers.GetTopSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offsetPosition,
                                    Helpers.GetBottomSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offsetPosition,
                                    scaledRadius,
                                    direction,
                                    out smallRadiusHitInfo,
                                    distance + extraDistance,
                                    collisionLayerMask,
                                    triggerQuery))
            {
                return smallRadiusHitInfo.distance <= distance;
            }

            return false;
        }

        // Do a capsule cast, includes the skin width.
        //      direction: Direction to cast.
        //      distance: Distance to cast.
        //      bigRadiusHitInfo: Hit info.
        //      offsetPosition: Offset position, if we want to test somewhere relative to the capsule's position.
        //      currentPosition: position of the character
        bool BigCapsuleCast(Vector3 direction, float distance,
                            out RaycastHit bigRadiusHitInfo,
                            Vector3 offsetPosition, Vector3 currentPosition)
        {
            // Cast further than the distance we need, to try to take into account small edge cases (e.g. Casts fail
            // when moving almost parallel to an obstacle for small distances).
            float extraDistance = scaledRadius + skinWidth;

            if (Physics.CapsuleCast(Helpers.GetTopSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offsetPosition,
                                    Helpers.GetBottomSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offsetPosition,
                                    scaledRadius + skinWidth,
                                    direction,
                                    out bigRadiusHitInfo,
                                    distance + extraDistance,
                                    collisionLayerMask,
                                    triggerQuery))
            {
                return bigRadiusHitInfo.distance <= distance;
            }

            return false;
        }

        // Do a sphere cast, excludes the skin width. Sphere position is at the top or bottom of the capsule.
        //      direction: Direction to cast.
        //      distance: Distance to cast.
        //      smallRadiusHitInfo: Hit info.
        //      offsetPosition: Offset position, if we want to test somewhere relative to the capsule's position.
        //      useBottomSphere: Use the sphere at the bottom of the capsule? If false then use the top sphere.
        //      currentPosition: position of the character
        bool SmallSphereCast(Vector3 direction, float distance,
                             out RaycastHit smallRadiusHitInfo,
                             Vector3 offsetPosition,
                             bool useBottomSphere, Vector3 currentPosition)
        {
            // Cast further than the distance we need, to try to take into account small edge cases (e.g. Casts fail
            // when moving almost parallel to an obstacle for small distances).
            float extraDistance = scaledRadius;

            Vector3 spherePosition = useBottomSphere ? Helpers.GetBottomSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offsetPosition
                                                     : Helpers.GetTopSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offsetPosition;
            if (Physics.SphereCast(spherePosition,
                                   scaledRadius,
                                   direction,
                                   out smallRadiusHitInfo,
                                   distance + extraDistance,
                                   collisionLayerMask,
                                   triggerQuery))
            {
                return smallRadiusHitInfo.distance <= distance;
            }

            return false;
        }

        // Do a sphere cast, including the skin width. Sphere position is at the top or bottom of the capsule.
        //      direction: Direction to cast.
        //      distance: Distance to cast.
        //      currentPosition: position of the character
        //      bigRadiusHitInfo: Hit info.
        //      offsetPosition: Offset position, if we want to test somewhere relative to the capsule's position.
        //      useBottomSphere: Use the sphere at the bottom of the capsule? If false then use the top sphere.
        bool BigSphereCast(Vector3 direction, float distance, Vector3 currentPosition,
                                   out RaycastHit bigRadiusHitInfo,
                                   Vector3 offsetPosition,
                                   bool useBottomSphere)
        {
            // Cast further than the distance we need, to try to take into account small edge cases (e.g. Casts fail
            // when moving almost parallel to an obstacle for small distances).
            float extraDistance = scaledRadius + skinWidth;

            Vector3 spherePosition = useBottomSphere ? Helpers.GetBottomSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offsetPosition
                                                     : Helpers.GetTopSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offsetPosition;
            if (Physics.SphereCast(spherePosition,
                                   scaledRadius + skinWidth,
                                   direction,
                                   out bigRadiusHitInfo,
                                   distance + extraDistance,
                                   collisionLayerMask,
                                   triggerQuery))
            {
                return bigRadiusHitInfo.distance <= distance;
            }

            return false;
        }

        // Called when a capsule cast detected an obstacle. Move away from the obstacle and slide against it if needed.
        //      moveVector: The movement vector.
        //      hitInfoCapsule: Hit info of the capsule cast collision.
        //      direction: Direction of the cast.
        //      distance: Distance of the cast.
        //      canSlide: Can slide against obstacles?
        //      tryGrounding: Try grounding the player?
        //      hitSmallCapsule: Did the collision occur with the small capsule (i.e. no skin width)?
        //      currentPosition: position of the character
        void MoveAwayFromObstacle(ref Vector3 moveVector, ref RaycastHit hitInfoCapsule,
                                  Vector3 direction, float distance,
                                  bool canSlide,
                                  bool tryGrounding,
                                  bool hitSmallCapsule, ref Vector3 currentPosition)
        {
            // IMPORTANT: This method must set moveVector.

            // When the small capsule hit then stop skinWidth away from obstacles
            float collisionOffset = hitSmallCapsule ? skinWidth : k_CollisionOffset;

            float hitDistance = Mathf.Max(hitInfoCapsule.distance - collisionOffset, 0.0f);
            // Note: remainingDistance is more accurate is we use hitDistance, but using hitInfoCapsule.distance gives a tiny
            // bit of dampening when sliding along obstacles
            float remainingDistance = Mathf.Max(distance - hitInfoCapsule.distance, 0.0f);

            // Move to the collision point
            MovePosition(direction * hitDistance, direction, hitInfoCapsule, ref currentPosition);

            Vector3 hitNormal;
            RaycastHit hitInfoRay;
            Vector3 rayOrigin = currentPosition + transformedCenter;
            Vector3 rayDirection = hitInfoCapsule.point - rayOrigin;

            // Raycast returns a more accurate normal than SphereCast/CapsuleCast
            // Using angle <= k_MaxAngleToUseRaycastNormal gives a curve when collision is near an edge.
            if (Physics.Raycast(rayOrigin,
                                rayDirection,
                                out hitInfoRay,
                                rayDirection.magnitude * k_RaycastScaleDistance,
                                collisionLayerMask,
                                triggerQuery) &&
                hitInfoRay.collider == hitInfoCapsule.collider &&
                Vector3.Angle(hitInfoCapsule.normal, hitInfoRay.normal) <= k_MaxAngleToUseRaycastNormal)
            {
                hitNormal = hitInfoRay.normal;
            }
            else
            {
                hitNormal = hitInfoCapsule.normal;
            }

            float penetrationDistance;
            Vector3 penetrationDirection;

            if (GetPenetrationInfo(out penetrationDistance, out penetrationDirection, currentPosition, true, null, hitInfoCapsule))
            {
                // Push away from the obstacle
                MovePosition(penetrationDirection * penetrationDistance, null, null, ref currentPosition);
            }

            bool slopeIsSteep = false;
            if (tryGrounding || m_StuckInfo.isStuck)
            {
                // No further movement when grounding the character, or the character is stuck
                canSlide = false;
            }
            else if (moveVector.x.NotEqualToZero() || moveVector.z.NotEqualToZero())
            {
                // Test if character is trying to walk up a steep slope
                float slopeAngle = Vector3.Angle(Vector3.up, hitNormal);
                slopeIsSteep = slopeAngle > slopeLimit && slopeAngle < k_MaxSlopeLimit && Vector3.Dot(direction, hitNormal) < 0.0f;
            }

            // Set moveVector
            if (canSlide && remainingDistance > 0.0f)
            {
                Vector3 slideNormal = hitNormal;

                if (slopeIsSteep && slideNormal.y > 0.0f)
                {
                    // Do not move up the slope
                    slideNormal.y = 0.0f;
                    slideNormal.Normalize();
                }

                // Vector to slide along the obstacle
                Vector3 project = Vector3.Cross(direction, slideNormal);
                project = Vector3.Cross(slideNormal, project);

                if (slopeIsSteep && project.y > 0.0f)
                {
                    // Do not move up the slope
                    project.y = 0.0f;
                }

                project.Normalize();

                // Slide along the obstacle
                bool isWallSlowingDown = slowAgainstWalls && minSlowAgainstWallsAngle < 90.0f;

                if (isWallSlowingDown)
                {
                    // Factor used to perform a slow down against the walls.
                    float invRescaleFactor = 1 / Mathf.Cos(minSlowAgainstWallsAngle * Mathf.Deg2Rad);

                    // Cosine of angle between the movement direction and the tangent is equivalent to the sin of
                    // the angle between the movement direction and the normal, which is the sliding component of
                    // our movement.
                    float cosine = Vector3.Dot(project, direction);
                    float slowDownFactor = Mathf.Clamp01(cosine * invRescaleFactor);

                    moveVector = project * (remainingDistance * slowDownFactor);
                }
                else
                {
                    // No slow down, keep the same speed even against walls.
                    moveVector = project * remainingDistance;
                }
            }
            else
            {
                // Stop current move loop vector
                moveVector = Vector3.zero;
            }

            if (direction.y < 0.0f && Mathf.Approximately(direction.x, 0.0f) && Mathf.Approximately(direction.z, 0.0f))
            {
                // This is used by the sliding down slopes
                m_DownCollisionNormal = hitNormal;
            }
        }

        // Check for collision penetration, then try to de-penetrate if there is collision.
        bool Depenetrate(ref Vector3 currentPosition)
        {
            float distance;
            Vector3 direction;
            if (GetPenetrationInfo(out distance, out direction, currentPosition))
            {
                MovePosition(direction * distance, null, null, ref currentPosition);
                return true;
            }

            return false;
        }

        // Get direction and distance to move out of the obstacle.
        //      getDistance: Get distance to move out of the obstacle.
        //      getDirection: Get direction to move out of the obstacle.
        //      currentPosition: position of the character
        //      includeSkinWidth: Include the skin width in the test?
        //      offsetPosition: Offset position, if we want to test somewhere relative to the capsule's position.
        //      hitInfo: The hit info.
        bool GetPenetrationInfo(out float getDistance, out Vector3 getDirection,
                                Vector3 currentPosition,
                                bool includeSkinWidth = true,
                                Vector3? offsetPosition = null,
                                RaycastHit? hitInfo = null)
        {
            getDistance = 0.0f;
            getDirection = Vector3.zero;

            Vector3 offset = offsetPosition != null ? offsetPosition.Value : Vector3.zero;
            float tempSkinWidth = includeSkinWidth ? skinWidth : 0.0f;
            int overlapCount = Physics.OverlapCapsuleNonAlloc(Helpers.GetTopSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offset,
                                                              Helpers.GetBottomSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offset,
                                                              scaledRadius + tempSkinWidth,
                                                              m_PenetrationInfoColliders,
                                                              collisionLayerMask,
                                                              triggerQuery);
            if (overlapCount <= 0 || m_PenetrationInfoColliders.Length <= 0)
            {
                return false;
            }

            bool result = false;
            Vector3 localPos = Vector3.zero;
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = m_PenetrationInfoColliders[i];
                if (col == null)
                {
                    break;
                }

                Vector3 direction;
                float distance;
                Transform colliderTransform = col.transform;
                if (ComputePenetration(offset,
                                       col, colliderTransform.position, colliderTransform.rotation,
                                       out direction, out distance, includeSkinWidth, currentPosition))
                {
                    localPos += direction * (distance + k_CollisionOffset);
                    result = true;
                }
                else if (hitInfo != null && hitInfo.Value.collider == col)
                {
                    // We can use the hit normal to push away from the collider, because CapsuleCast generally returns a normal
                    // that pushes away from the collider.
                    localPos += hitInfo.Value.normal * k_CollisionOffset;
                    result = true;
                }
            }

            if (result)
            {
                getDistance = localPos.magnitude;
                getDirection = localPos.normalized;
            }

            return result;
        }

        // Check if any colliders overlap the capsule.
        //      includeSkinWidth: Include the skin width in the test?
        //      offsetPosition: Offset position, if we want to test somewhere relative to the capsule's position.
        bool CheckCapsule(Vector3 currentPosition, bool includeSkinWidth = true,
                          Vector3? offsetPosition = null)
        {
            Vector3 offset = offsetPosition != null ? offsetPosition.Value : Vector3.zero;
            float tempSkinWidth = includeSkinWidth ? skinWidth : 0;
            return Physics.CheckCapsule(Helpers.GetTopSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offset,
                                        Helpers.GetBottomSphereWorldPosition(currentPosition, transformedCenter, scaledRadius, scaledHeight) + offset,
                                        scaledRadius + tempSkinWidth,
                                        collisionLayerMask,
                                        triggerQuery);
        }

        // Move the capsule position.
        //      moveVector: Move vector.
        //      collideDirection: Direction we encountered collision. Null if no collision.
        //      hitInfo: Hit info of the collision. Null if no collision.
        //      currentPosition: position of the character
        void MovePosition(Vector3 moveVector, Vector3? collideDirection, RaycastHit? hitInfo, ref Vector3 currentPosition)
        {
            if (moveVector.sqrMagnitude.NotEqualToZero())
            {
                currentPosition += moveVector;
            }

            if (collideDirection != null && hitInfo != null)
            {
                UpdateCollisionInfo(collideDirection.Value, hitInfo.Value, currentPosition);
            }
        }

        // Update the collision flags and info.
        //      direction: The direction moved.
        //      hitInfo: The hit info of the collision.
        //      currentPosition: position of the character
        void UpdateCollisionInfo(Vector3 direction, RaycastHit? hitInfo, Vector3 currentPosition)
        {
            if (direction.x.NotEqualToZero() || direction.z.NotEqualToZero())
            {
                collisionFlags |= CollisionFlags.Sides;
            }

            if (direction.y > 0.0f)
            {
                collisionFlags |= CollisionFlags.CollidedAbove;
            }
            else if (direction.y < 0.0f)
            {
                collisionFlags |= CollisionFlags.CollidedBelow;
            }

            m_StuckInfo.hitCount++;

            if (hitInfo != null)
            {
                Collider collider = hitInfo.Value.collider;

                // We only care about the first collision with a collider
                if (!m_CollisionInfoDictionary.ContainsKey(collider))
                {
                    Vector3 moved = currentPosition - m_StartPosition;
                    CollisionInfo newCollisionInfo = new CollisionInfo(this, hitInfo.Value, direction, moved.magnitude);
                    m_CollisionInfoDictionary.Add(collider, newCollisionInfo);
                }
            }
        }

        // do different casts downwards to find the best slope normal
        bool CastForSlopeNormal(out Vector3 slopeNormal)
        {

            // sphere down to hit slope
            RaycastHit hitInfoSphere;
            if (!SmallSphereCast(Vector3.down,
                                 skinWidth + k_SlideDownSlopeTestDistance,
                                 out hitInfoSphere,
                                 Vector3.zero,
                                 true, transform.position))
            {
                // no slope found, not sliding anymore
                slopeNormal = Vector3.zero;
                return false;
            }

            RaycastHit hitInfoRay;
            Vector3 rayOrigin = Helpers.GetBottomSphereWorldPosition(transform.position, transformedCenter, scaledRadius, scaledHeight);
            Vector3 rayDirection = hitInfoSphere.point - rayOrigin;

            // there is a slope below us.
            // let's raycast again for a more accurate normal than spherecast/capsulecast
            if (Physics.Raycast(rayOrigin,
                                rayDirection,
                                out hitInfoRay,
                                rayDirection.magnitude * k_RaycastScaleDistance,
                                collisionLayerMask,
                                triggerQuery) &&
                hitInfoRay.collider == hitInfoSphere.collider)
            {
                // raycast hit something, so we have a more accurate normal now
                slopeNormal = hitInfoRay.normal;
            }
            else
            {
                // raycast hit nothing. let's keep the first normal.
                slopeNormal = hitInfoSphere.normal;
            }
            return true;
        }

        // get best slope normal from either:
        // * reusing last move's down collision normal IF sliding already
        // * or casting for slope normal if NOT sliding yet
        bool ReuseOrCastForSlopeNormal(out Vector3 slopeNormal)
        {
            // if we are about to slide, or currently sliding, then we already
            // moved down and collided with the slope's surface. in that case,
            // simply reuse the normal instead of casting again.
            // IMPORTANT: we are NOT on a slope surface if state == Stopped!
            bool onSlopeSurface = slidingState == SlidingState.STARTING ||
                                  slidingState == SlidingState.SLIDING;
            if (onSlopeSurface && m_DownCollisionNormal != null)
            {
                slopeNormal = m_DownCollisionNormal.Value;
                return true;
            }
            // otherwise sphere/raycast to find a really good slope normal
            else if (CastForSlopeNormal(out slopeNormal))
            {
                return true;
            }
            // no slope found
            return false;
        }

        public static bool IsSlideableAngle(float slopeAngle, float slopeLimit)
        {
            // needs to be between slopeLimit (to start sliding) and maxSlide
            // (to stop sliding)
            return slopeLimit <= slopeAngle && slopeAngle < k_MaxSlideAngle;
        }

        // calculate y (down) component of slide move
        public static float CalculateSlideVerticalVelocity(Vector3 slopeNormal, float slidingTime, float slideGravityMultiplier, float slideMaxSpeed)
        {
            // calculate slope angle
            float slopeAngle = Vector3.Angle(Vector3.up, slopeNormal);

            // Speed increases as slope angle increases
            float slideSpeedScale = Mathf.Clamp01(slopeAngle / k_MaxSlideAngle);

            // gravity depends on Physics.gravity and how steep the slide is
            float gravity = Mathf.Abs(Physics.gravity.y) * slideGravityMultiplier * slideSpeedScale;

            // Apply gravity and slide along the obstacle
            // -> multiplied by slidingTime so it gets faster the longer we slide
            return -Mathf.Clamp(gravity * slidingTime, 0, Mathf.Abs(slideMaxSpeed));
        }

        // helper function to do the actual sliding move
        // returns true if we did slide. false otherwise.
        bool DoSlideMove(Vector3 slopeNormal, float slidingTimeElapsed)
        {
            // calculate slide velocity Y based on parameters
            float velocityY = CalculateSlideVerticalVelocity(slopeNormal, slidingTimeElapsed, slideGravityMultiplier, slideMaxSpeed);

            // multiply with deltaTime so it's frame rate independent
            velocityY *= Time.deltaTime;

            // Push slightly away from the slope
            // => not multiplied by deltaTime because we stay away 'pushDistance'
            //    from the slope surface at all times
            Vector3 push = new Vector3(slopeNormal.x, 0, slopeNormal.z).normalized * k_PushAwayFromSlopeDistance;
            Vector3 moveVector = new Vector3(push.x, velocityY, push.z);

            // Preserve collision flags and velocity. Because user expects them to only be set when manually calling Move/SimpleMove.
            CollisionFlags oldCollisionFlags = collisionFlags;
            Vector3 oldVelocity = velocity;

            // move along the slope
            bool didSlide = true;
            MoveInternal(moveVector, true, true, true);
            if ((collisionFlags & CollisionFlags.CollidedSides) != 0)
            {
                // Stop sliding when hit something on the side
                didSlide = false;
            }

            // restore collision flags and velocity
            collisionFlags = oldCollisionFlags;
            velocity = oldVelocity;

            // return result
            return didSlide;
        }

        // sliding FSM state update
        SlidingState UpdateSlidingNONE()
        {
            // find slope normal by reusing or casting for a new one
            // AND check if valid angle
            if (ReuseOrCastForSlopeNormal(out Vector3 slopeNormal) &&
                IsSlideableAngle(Vector3.Angle(Vector3.up, slopeNormal), slopeLimit))
            {
                // we are definitely on a slide.
                // sometimes, we are running over tiny slides, but we
                // shouldn't immediately start sliding every time.
                // only after a 'slideStartTime'
                // (originally slideStartTime was completely ignored.
                //  this fixes it.)
                //Debug.Log("Considering sliding for slope with angle: " + Vector3.Angle(Vector3.up, slopeNormal));
                slidingStartedTime = Time.time;
                return SlidingState.STARTING;
            }
            // if none is found, then we just aren't sliding
            else return SlidingState.NONE;
        }

        // sliding FSM state update
        SlidingState UpdateSlidingSTARTING()
        {
            // find slope normal by reusing or casting for a new one
            // AND check if valid angle
            if (ReuseOrCastForSlopeNormal(out Vector3 slopeNormal) &&
                IsSlideableAngle(Vector3.Angle(Vector3.up, slopeNormal), slopeLimit))
            {
                // we are still on a slope that will cause sliding.
                // but wait until start time has elapsed.
                if (Time.time >= slidingStartedTime + slideStartDelay)
                {
                    // actually start sliding in the next frame
                    //Debug.LogWarning("Starting sliding for slope with angle: " + Vector3.Angle(Vector3.up, slopeNormal) + " after on it for " + slideStartDelay + " seconds");
                    return SlidingState.SLIDING;
                }
                // otherwise wait a little longer
                else return SlidingState.STARTING;
            }
            // if none is found, then we briefly walked over a slope, but not
            // long enough to actually start sliding
            else
            {
                return SlidingState.NONE;
            }
        }

        // sliding FSM state update
        SlidingState UpdateSlidingSLIDING()
        {
            // find slope normal by reusing or casting for a new one
            // AND check if valid angle
            if (ReuseOrCastForSlopeNormal(out Vector3 slopeNormal) &&
                IsSlideableAngle(Vector3.Angle(Vector3.up, slopeNormal), slopeLimit))
            {
                // Pro tip: Here you can also use the friction of the physics material of the slope, to adjust the slide speed.

                // find out how long we have been sliding for.
                // speed gets faster the longer we have been sliding.
                float slidingTimeElapsed = Time.time - slidingStartedTime;

                // do the slide move
                // if we slided, then keep sliding
                if (DoSlideMove(slopeNormal, slidingTimeElapsed))
                {
                    return SlidingState.SLIDING;
                }
                // if we collided on the side,  we transition to stopping
                else
                {
                    slidingStoppedTime = Time.time;
                    return SlidingState.STOPPING;
                }
            }
            // if none is found, then we transition to stopping
            else
            {
                //Debug.LogWarning("Sliding->Stopping");
                slidingStoppedTime = Time.time;
                return SlidingState.STOPPING;
            }
        }

        // sliding FSM state update
        SlidingState UpdateSlidingSTOPPING()
        {
            // find slope normal by reusing or casting for a new one
            // AND check if valid angle
            if (ReuseOrCastForSlopeNormal(out Vector3 slopeNormal) &&
                IsSlideableAngle(Vector3.Angle(Vector3.up, slopeNormal), slopeLimit))
            {
                // we found a new one even though we were about to stop.
                // in that case, continue sliding in the next frame.
                return SlidingState.SLIDING;
            }
            // not on a slope and enough time elapsed to stop?
            // this is necessary for two reason:
            // * so we don't immediately stop to slide when sliding over a
            //   tiny flat surface
            // * so we don't allow jumping immediately after sliding, which
            //   is useful in some games.
            else if (Time.time >= slidingStoppedTime + slideStopDelay)
            {
                //Debug.LogWarning("Stopping sliding after not on a slope for " + slideStopDelay + " seconds");
                return SlidingState.NONE;
            }
            // not on a slope, but not enough time elapsed.
            // wait a little longer.
            else
            {
                return SlidingState.STOPPING;
            }
        }

        // Auto-slide down steep slopes.
        void UpdateSlideDownSlopes()
        {
            // only if sliding feature enabled, and if on ground
            if (!slideDownSlopes || !isGrounded)
            {
                slidingState = SlidingState.NONE;
                return;
            }

            // sliding mechanics are complex. we need a state machine to keep
            // it simple, understandable and modifiable.
            // (previously it used complex if/else cases, which were hard to
            //  understand and hard to modify/debug)
            if      (slidingState == SlidingState.NONE)     slidingState = UpdateSlidingNONE();
            else if (slidingState == SlidingState.STARTING) slidingState = UpdateSlidingSTARTING();
            else if (slidingState == SlidingState.SLIDING)  slidingState = UpdateSlidingSLIDING();
            else if (slidingState == SlidingState.STOPPING) slidingState = UpdateSlidingSTOPPING();
            else Debug.LogError("Unhandled sliding state: " + slidingState);
        }

        // Sets the playerRootTransform's localPosition to the rootTransformOffset
        void SetRootToOffset()
        {
            if (playerRootTransform != null)
            {
                playerRootTransform.localPosition = rootTransformOffset;
            }
        }
    }
}