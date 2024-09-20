// PredictedRigidbody which stores & indidvidually rewinds history per Rigidbody.
//
// This brings significant performance savings because:
// - if a scene has 1000 objects
// - and a player interacts with say 3 objects at a time
// - Physics.Simulate() would resimulate 1000 objects
// - where as this component only resimulates the 3 changed objects
//
// The downside is that history rewinding is done manually via Vector math,
// instead of real physics. It's not 100% correct - but it sure is fast!
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    public enum PredictionMode { Smooth, Fast }

    // [RequireComponent(typeof(Rigidbody))] <- RB is moved out at runtime, can't require it.
    public class PredictedRigidbody : NetworkBehaviour
    {
        Transform tf; // this component is performance critical. cache .transform getter!

        // Prediction sometimes moves the Rigidbody to a ghost object.
        // .predictedRigidbody is always kept up to date to wherever the RB is.
        // other components should use this when accessing Rigidbody.
        public Rigidbody predictedRigidbody;
        Transform predictedRigidbodyTransform; // predictedRigidbody.transform for performance (Get/SetPositionAndRotation)

        Vector3 lastPosition;

        // motion smoothing happen on-demand, because it requires moving physics components to another GameObject.
        // this only starts at a given velocity and ends when stopped moving.
        // to avoid constant on/off/on effects, it also stays on for a minimum time.
        [Header("Motion Smoothing")]
        [Tooltip("Prediction supports two different modes: Smooth and Fast:\n\nSmooth: Physics are separated from the GameObject & applied in the background. Rendering smoothly follows the physics for perfectly smooth interpolation results. Much softer, can be even too soft where sharp collisions won't look as sharp (i.e. Billiard balls avoid the wall before even hitting it).\n\nFast: Physics remain on the GameObject and corrections are applied hard. Much faster since we don't need to update a separate GameObject, a bit harsher, more precise.")]
        public PredictionMode mode = PredictionMode.Smooth;
        [Tooltip("Smoothing via Ghost-following only happens on demand, while moving with a minimum velocity.")]
        public float motionSmoothingVelocityThreshold = 0.1f;
        float motionSmoothingVelocityThresholdSqr; // ² cached in Awake
        public float motionSmoothingAngularVelocityThreshold = 5.0f; // Billiards demo: 0.1 is way too small, takes forever for IsMoving()==false
        float motionSmoothingAngularVelocityThresholdSqr; // ² cached in Awake
        public float motionSmoothingTimeTolerance = 0.5f;
        double motionSmoothingLastMovedTime;

        // client keeps state history for correction & reconciliation.
        // this needs to be a SortedList because we need to be able to insert inbetween.
        // => RingBuffer: see prediction_ringbuffer_2 branch, but it's slower!
        [Header("State History")]
        public int stateHistoryLimit = 32; // 32 x 50 ms = 1.6 seconds is definitely enough
        readonly SortedList<double, RigidbodyState> stateHistory = new SortedList<double, RigidbodyState>();
        public float recordInterval = 0.050f;

        [Tooltip("(Optional) performance optimization where FixedUpdate.RecordState() only inserts state into history if the state actually changed.\nThis is generally a good idea.")]
        public bool onlyRecordChanges = true;

        [Tooltip("(Optional) performance optimization where received state is compared to the LAST recorded state first, before sampling the whole history.\n\nThis can save significant traversal overhead for idle objects with a tiny chance of missing corrections for objects which revisisted the same position in the recent history twice.")]
        public bool compareLastFirst = true;

        [Header("Reconciliation")]
        [Tooltip("Correction threshold in meters. For example, 0.1 means that if the client is off by more than 10cm, it gets corrected.")]
        public double positionCorrectionThreshold = 0.10;
        double positionCorrectionThresholdSqr; // ² cached in Awake
        [Tooltip("Correction threshold in degrees. For example, 5 means that if the client is off by more than 5 degrees, it gets corrected.")]
        public double rotationCorrectionThreshold = 5;

        [Tooltip("Applying server corrections one frame ahead gives much better results. We don't know why yet, so this is an option for now.")]
        public bool oneFrameAhead = true;

        [Header("Smoothing")]
        [Tooltip("Snap to the server state directly when velocity is < threshold. This is useful to reduce jitter/fighting effects before coming to rest.\nNote this applies position, rotation and velocity(!) so it's still smooth.")]
        public float snapThreshold = 2; // 0.5 has too much fighting-at-rest, 2 seems ideal.

        [Header("Visual Interpolation")]
        [Tooltip("After creating the visual interpolation object, keep showing the original Rigidbody with a ghost (transparent) material for debugging.")]
        public bool showGhost = true;
        [Tooltip("Physics components are moved onto a ghost object beyond this threshold. Main object visually interpolates to it.")]
        public float ghostVelocityThreshold = 0.1f;

        [Tooltip("After creating the visual interpolation object, replace this object's renderer materials with the ghost (ideally transparent) material.")]
        public Material localGhostMaterial;
        public Material remoteGhostMaterial;

        [Tooltip("Performance optimization: only create/destroy ghosts every n-th frame is enough.")]
        public int checkGhostsEveryNthFrame = 4;

        [Tooltip("How fast to interpolate to the target position, relative to how far we are away from it.\nHigher value will be more jitter but sharper moves, lower value will be less jitter but a little too smooth / rounded moves.")]
        public float positionInterpolationSpeed = 15; // 10 is a little too low for billiards at least
        public float rotationInterpolationSpeed = 10;

        [Tooltip("Teleport if we are further than 'multiplier x collider size' behind.")]
        public float teleportDistanceMultiplier = 10;

        [Header("Bandwidth")]
        [Tooltip("Reduce sends while velocity==0. Client's objects may slightly move due to gravity/physics, so we still want to send corrections occasionally even if an object is idle on the server the whole time.")]
        public bool reduceSendsWhileIdle = true;

        // Rigidbody & Collider are moved out into a separate object.
        // this way the visual object can smoothly follow.
        protected GameObject physicsCopy;
        // protected Transform physicsCopyTransform; // caching to avoid GetComponent
        // protected Rigidbody physicsCopyRigidbody => rb; // caching to avoid GetComponent
        // protected Collider physicsCopyCollider;   // caching to avoid GetComponent
        float smoothFollowThreshold; // caching to avoid calculation in LateUpdate
        float smoothFollowThresholdSqr; // caching to avoid calculation in LateUpdate

        // we also create one extra ghost for the exact known server state.
        protected GameObject remoteCopy;

        // joints
        Vector3 initialPosition;
        Quaternion initialRotation;
        // Vector3 initialScale; // don't change scale for now. causes issues with parenting.

        Color originalColor;

        protected virtual void Awake()
        {
            tf = transform;
            predictedRigidbody = GetComponent<Rigidbody>();
            if (predictedRigidbody == null) throw new InvalidOperationException($"Prediction: {name} is missing a Rigidbody component.");
            predictedRigidbodyTransform = predictedRigidbody.transform;

            // in fast mode, we need to force enable Rigidbody.interpolation.
            // otherwise there's not going to be any smoothing whatsoever.
            if (mode == PredictionMode.Fast)
            {
                predictedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // cache some threshold to avoid calculating them in LateUpdate
            float colliderSize = GetComponentInChildren<Collider>().bounds.size.magnitude;
            smoothFollowThreshold = colliderSize * teleportDistanceMultiplier;
            smoothFollowThresholdSqr = smoothFollowThreshold * smoothFollowThreshold;

            // cache initial position/rotation/scale to be used when moving physics components (configurable joints' range of motion)
            initialPosition = tf.position;
            initialRotation = tf.rotation;
            // initialScale = tf.localScale;

            // cache ² computations
            motionSmoothingVelocityThresholdSqr = motionSmoothingVelocityThreshold * motionSmoothingVelocityThreshold;
            motionSmoothingAngularVelocityThresholdSqr = motionSmoothingAngularVelocityThreshold * motionSmoothingAngularVelocityThreshold;
            positionCorrectionThresholdSqr = positionCorrectionThreshold * positionCorrectionThreshold;
        }

        protected virtual void CopyRenderersAsGhost(GameObject destination, Material material)
        {
            // find the MeshRenderer component, which sometimes is on a child.
            MeshRenderer originalMeshRenderer = GetComponentInChildren<MeshRenderer>(true);
            MeshFilter originalMeshFilter = GetComponentInChildren<MeshFilter>(true);
            if (originalMeshRenderer != null && originalMeshFilter != null)
            {
                MeshFilter meshFilter = destination.AddComponent<MeshFilter>();
                meshFilter.mesh = originalMeshFilter.mesh;

                MeshRenderer meshRenderer = destination.AddComponent<MeshRenderer>();
                meshRenderer.material = originalMeshRenderer.material;

                // renderers often have multiple materials. copy all.
                if (originalMeshRenderer.materials != null)
                {
                    Material[] materials = new Material[originalMeshRenderer.materials.Length];
                    for (int i = 0; i < materials.Length; ++i)
                    {
                        materials[i] = material;
                    }
                    meshRenderer.materials = materials; // need to reassign to see it in effect
                }
            }
            // if we didn't find a renderer, show a warning
            else Debug.LogWarning($"PredictedRigidbody: {name} found no renderer to copy onto the visual object. If you are using a custom setup, please overwrite PredictedRigidbody.CreateVisualCopy().");
        }

        // instantiate a physics-only copy of the gameobject to apply corrections.
        // this way the main visual object can smoothly follow.
        // it's best to separate the physics instead of separating the renderers.
        // some projects have complex rendering / animation setups which we can't touch.
        // besides, Rigidbody+Collider are two components, where as renders may be many.
        protected virtual void CreateGhosts()
        {
            // skip if host mode or already separated
            if (isServer || physicsCopy != null) return;

            // Debug.Log($"Separating Physics for {name}"); // logging this allocates too much

            // create an empty GameObject with the same name + _Physical
            // it's important to copy world position/rotation/scale, not local!
            // because the original object may be a child of another.
            //
            // for example:
            //    parent (scale=1.5)
            //      child (scale=0.5)
            //
            // if we copy localScale then the copy has scale=0.5, where as the
            // original would have a global scale of ~1.0.
            physicsCopy = new GameObject($"{name}_Physical");

            // assign the same Layer for the physics copy.
            // games may use a custom physics collision matrix, layer matters.
            physicsCopy.layer = gameObject.layer;

            // add the PredictedRigidbodyPhysical component
            PredictedRigidbodyPhysicsGhost physicsGhostRigidbody = physicsCopy.AddComponent<PredictedRigidbodyPhysicsGhost>();
            physicsGhostRigidbody.target = tf;

            // when moving (Configurable)Joints, their range of motion is
            // relative to the initial position. if we move them after the
            // GameObject rotated, the range of motion is wrong.
            // the easiest solution is to move to initial position,
            // then move physics components, then move back.
            // => remember previous
            Vector3 position = tf.position;
            Quaternion rotation = tf.rotation;
            // Vector3 scale = tf.localScale; // don't change scale for now. causes issues with parenting.
            // => reset to initial
            physicsGhostRigidbody.transform.position = tf.position = initialPosition;
            physicsGhostRigidbody.transform.rotation = tf.rotation = initialRotation;
            physicsGhostRigidbody.transform.localScale = tf.lossyScale;// world scale! // = initialScale; // don't change scale for now. causes issues with parenting.
            // => move physics components
            PredictionUtils.MovePhysicsComponents(gameObject, physicsCopy);
            // => reset previous
            physicsGhostRigidbody.transform.position = tf.position = position;
            physicsGhostRigidbody.transform.rotation = tf.rotation = rotation;
            //physicsGhostRigidbody.transform.localScale = tf.lossyScale; // world scale! //= scale; // don't change scale for now. causes issues with parenting.

            // show ghost by copying all renderers / materials with ghost material applied
            if (showGhost)
            {
                // one for the locally predicted rigidbody
                CopyRenderersAsGhost(physicsCopy, localGhostMaterial);

                // one for the latest remote state for comparison
                // it's important to copy world position/rotation/scale, not local!
                // because the original object may be a child of another.
                //
                // for example:
                //    parent (scale=1.5)
                //      child (scale=0.5)
                //
                // if we copy localScale then the copy has scale=0.5, where as the
                // original would have a global scale of ~1.0.
                remoteCopy = new GameObject($"{name}_Remote");
                remoteCopy.transform.position = tf.position;     // world position!
                remoteCopy.transform.rotation = tf.rotation;     // world rotation!
                remoteCopy.transform.localScale = tf.lossyScale; // world scale!
                CopyRenderersAsGhost(remoteCopy, remoteGhostMaterial);
            }

            // assign our Rigidbody reference to the ghost
            predictedRigidbody = physicsCopy.GetComponent<Rigidbody>();
            predictedRigidbodyTransform = predictedRigidbody.transform;
        }

        protected virtual void DestroyGhosts()
        {
            // move the copy's Rigidbody back onto self.
            // important for scene objects which may be reused for AOI spawn/despawn.
            // otherwise next time they wouldn't have a collider anymore.
            if (physicsCopy != null)
            {
                // when moving (Configurable)Joints, their range of motion is
                // relative to the initial position. if we move them after the
                // GameObject rotated, the range of motion is wrong.
                // the easiest solution is to move to initial position,
                // then move physics components, then move back.
                // => remember previous
                Vector3 position = tf.position;
                Quaternion rotation = tf.rotation;
                Vector3 scale = tf.localScale;
                // => reset to initial
                physicsCopy.transform.position = tf.position = initialPosition;
                physicsCopy.transform.rotation = tf.rotation = initialRotation;
                physicsCopy.transform.localScale = tf.lossyScale;// = initialScale;
                // => move physics components
                PredictionUtils.MovePhysicsComponents(physicsCopy, gameObject);
                // => reset previous
                tf.position = position;
                tf.rotation = rotation;
                tf.localScale = scale;

                // when moving components back, we need to undo the joints initial-delta rotation that we added.
                Destroy(physicsCopy);

                // reassign our Rigidbody reference
                predictedRigidbody = GetComponent<Rigidbody>();
                predictedRigidbodyTransform = predictedRigidbody.transform;
            }

            // simply destroy the remote copy
            if (remoteCopy != null) Destroy(remoteCopy);
        }

        // this shows in profiler LateUpdates! need to make this as fast as possible!
        protected virtual void SmoothFollowPhysicsCopy()
        {
            // hard follow:
            // predictedRigidbodyTransform.GetPositionAndRotation(out Vector3 physicsPosition, out Quaternion physicsRotation);
            // tf.SetPositionAndRotation(physicsPosition, physicsRotation);

            // ORIGINAL VERSION: CLEAN AND SIMPLE
            /*
            // if we are further than N colliders sizes behind, then teleport
            float colliderSize = physicsCopyCollider.bounds.size.magnitude;
            float threshold = colliderSize * teleportDistanceMultiplier;
            float distance = Vector3.Distance(tf.position, physicsCopyRigidbody.position);
            if (distance > threshold)
            {
                tf.position = physicsCopyRigidbody.position;
                tf.rotation = physicsCopyRigidbody.rotation;
                Debug.Log($"[PredictedRigidbody] Teleported because distance to physics copy = {distance:F2} > threshold {threshold:F2}");
                return;
            }

            // smoothly interpolate to the target position.
            // speed relative to how far away we are
            float positionStep = distance * positionInterpolationSpeed;
            tf.position = Vector3.MoveTowards(tf.position, physicsCopyRigidbody.position, positionStep * Time.deltaTime);

            // smoothly interpolate to the target rotation.
            // Quaternion.RotateTowards doesn't seem to work at all, so let's use SLerp.
            tf.rotation = Quaternion.Slerp(tf.rotation, physicsCopyRigidbody.rotation, rotationInterpolationSpeed * Time.deltaTime);
            */

            // FAST VERSION: this shows in profiler a lot, so cache EVERYTHING!
            tf.GetPositionAndRotation(out Vector3 currentPosition, out Quaternion currentRotation); // faster than tf.position + tf.rotation
            predictedRigidbodyTransform.GetPositionAndRotation(out Vector3 physicsPosition, out Quaternion physicsRotation); // faster than Rigidbody .position and .rotation
            float deltaTime = Time.deltaTime;

            // slow and simple version:
            //   float distance = Vector3.Distance(currentPosition, physicsPosition);
            //   if (distance > smoothFollowThreshold)
            // faster version
            Vector3 delta = physicsPosition - currentPosition;
            float sqrDistance = Vector3.SqrMagnitude(delta);
            float distance = Mathf.Sqrt(sqrDistance);
            if (sqrDistance > smoothFollowThresholdSqr)
            {
                tf.SetPositionAndRotation(physicsPosition, physicsRotation); // faster than .position and .rotation manually
                Debug.Log($"[PredictedRigidbody] Teleported because distance to physics copy = {distance:F2} > threshold {smoothFollowThreshold:F2}");
                return;
            }

            // smoothly interpolate to the target position.
            // speed relative to how far away we are.
            // => speed increases by distance² because the further away, the
            //    sooner we need to catch the fuck up
            // float positionStep = (distance * distance) * interpolationSpeed;
            float positionStep = distance * positionInterpolationSpeed;

            Vector3 newPosition = MoveTowardsCustom(currentPosition, physicsPosition, delta, sqrDistance, distance, positionStep * deltaTime);

            // smoothly interpolate to the target rotation.
            // Quaternion.RotateTowards doesn't seem to work at all, so let's use SLerp.
            // Quaternions always need to be normalized in order to be a valid rotation after operations
            Quaternion newRotation = Quaternion.Slerp(currentRotation, physicsRotation, rotationInterpolationSpeed * deltaTime).normalized;

            // assign position and rotation together. faster than accessing manually.
            tf.SetPositionAndRotation(newPosition, newRotation);
        }

        // simple and slow version with MoveTowards, which recalculates delta and delta.sqrMagnitude:
        //   Vector3 newPosition = Vector3.MoveTowards(currentPosition, physicsPosition, positionStep * deltaTime);
        // faster version copied from MoveTowards:
        // this increases Prediction Benchmark Client's FPS from 615 -> 640.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector3 MoveTowardsCustom(
            Vector3 current,
            Vector3 target,
            Vector3 _delta,     // pass this in since we already calculated it
            float _sqrDistance, // pass this in since we already calculated it
            float _distance,    // pass this in since we already calculated it
            float maxDistanceDelta)
        {
            if (_sqrDistance == 0.0 || maxDistanceDelta >= 0.0 && _sqrDistance <= maxDistanceDelta * maxDistanceDelta)
                return target;

            float distFactor = maxDistanceDelta / _distance; // unlike Vector3.MoveTowards, we only calculate this once
            return new Vector3(
                // current.x + (_delta.x / _distance) * maxDistanceDelta,
                // current.y + (_delta.y / _distance) * maxDistanceDelta,
                // current.z + (_delta.z / _distance) * maxDistanceDelta);
                current.x + _delta.x * distFactor,
                current.y + _delta.y * distFactor,
                current.z + _delta.z * distFactor);
        }

        // destroy visual copy only in OnStopClient().
        // OnDestroy() wouldn't be called for scene objects that are only disabled instead of destroyed.
        public override void OnStopClient()
        {
            DestroyGhosts();
        }

        void UpdateServer()
        {
            // bandwidth optimization while idle.
            if (reduceSendsWhileIdle)
            {
                // while moving, always sync every frame for immediate corrections.
                // while idle, only sync once per second.
                //
                // we still need to sync occasionally because objects on client
                // may still slide or move slightly due to gravity, physics etc.
                // and those still need to get corrected if not moving on server.
                //
                // TODO
                // next round of optimizations: if client received nothing for 1s,
                // force correct to last received state. then server doesn't need
                // to send once per second anymore.
                syncInterval = IsMoving() ? 0 : 1;
            }

            // always set dirty to always serialize in next sync interval.
            SetDirty();
        }

        // movement detection is virtual, in case projects want to use other methods.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual bool IsMoving() =>
            // straight forward implementation
            //   predictedRigidbody.velocity.magnitude >= motionSmoothingVelocityThreshold ||
            //   predictedRigidbody.angularVelocity.magnitude >= motionSmoothingAngularVelocityThreshold;
            // faster implementation with cached ²
#if UNITY_6000_0_OR_NEWER
            predictedRigidbody.linearVelocity.sqrMagnitude >= motionSmoothingVelocityThresholdSqr ||
#else
            predictedRigidbody.velocity.sqrMagnitude >= motionSmoothingVelocityThresholdSqr ||
#endif
            predictedRigidbody.angularVelocity.sqrMagnitude >= motionSmoothingAngularVelocityThresholdSqr;
        // TODO maybe merge the IsMoving() checks & callbacks with UpdateState().
        void UpdateGhosting()
        {
            // perf: enough to check ghosts every few frames.
            // PredictionBenchmark: only checking every 4th frame: 585 => 600 FPS
            if (Time.frameCount % checkGhostsEveryNthFrame != 0) return;

            // client only uses ghosts on demand while interacting.
            // this way 1000 GameObjects don't need +1000 Ghost GameObjects all the time!

            // no ghost at the moment
            if (physicsCopy == null)
            {
                // faster than velocity threshold? then create the ghosts.
                // with 10% buffer zone so we don't flip flop all the time.
                if (IsMoving())
                {
                    CreateGhosts();
                    OnBeginPrediction();
                }
            }
            // ghosting at the moment
            else
            {
                // always set last moved time while moving.
                // this way we can avoid on/off/oneffects when  stopping.
                if (IsMoving())
                {
                    motionSmoothingLastMovedTime = NetworkTime.time;
                }
                // slower than velocity threshold? then destroy the ghosts.
                // with a minimum time since starting to move, to avoid on/off/on effects.
                else
                {
                    if (NetworkTime.time >= motionSmoothingLastMovedTime + motionSmoothingTimeTolerance)
                    {
                        DestroyGhosts();
                        OnEndPrediction();
                        physicsCopy = null; // TESTING
                    }
                }
            }
        }

        // when using Fast mode, we don't create any ghosts.
        // but we still want to check IsMoving() in order to support the same
        // user callbacks.
        bool lastMoving = false;
        void UpdateState()
        {
            // perf: enough to check ghosts every few frames.
            // PredictionBenchmark: only checking every 4th frame: 770 => 800 FPS
            if (Time.frameCount % checkGhostsEveryNthFrame != 0) return;

            bool moving = IsMoving();

            // started moving?
            if (moving && !lastMoving)
            {
                OnBeginPrediction();
                lastMoving = true;
            }
            // stopped moving?
            else if (!moving && lastMoving)
            {
                // ensure a minimum time since starting to move, to avoid on/off/on effects.
                if (NetworkTime.time >= motionSmoothingLastMovedTime + motionSmoothingTimeTolerance)
                {
                    OnEndPrediction();
                    lastMoving = false;
                }
            }
        }

        void Update()
        {
            if (isServer) UpdateServer();
            if (isClientOnly)
            {
                 if (mode == PredictionMode.Smooth)
                    UpdateGhosting();
                 else if (mode == PredictionMode.Fast)
                    UpdateState();
            }
        }

        void LateUpdate()
        {
            // only follow on client-only, not in server or host mode
            if (isClientOnly && mode == PredictionMode.Smooth && physicsCopy) SmoothFollowPhysicsCopy();
        }

        void FixedUpdate()
        {
            // on clients (not host) we record the current state every FixedUpdate.
            // this is cheap, and allows us to keep a dense history.
            if (!isClientOnly) return;

            // OPTIMIZATION: RecordState() is expensive because it inserts into a SortedList.
            // only record if state actually changed!
            // risks not having up to date states when correcting,
            // but it doesn't matter since we'll always compare with the 'newest' anyway.
            //
            // we check in here instead of in RecordState() because RecordState() should definitely record if we call it!
            if (onlyRecordChanges)
            {
                // TODO maybe don't reuse the correction thresholds?
                tf.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
                // clean & simple:
                // if (Vector3.Distance(lastRecorded.position, position) < positionCorrectionThreshold &&
                //     Quaternion.Angle(lastRecorded.rotation, rotation) < rotationCorrectionThreshold)
                // faster:
                if ((lastRecorded.position - position).sqrMagnitude < positionCorrectionThresholdSqr &&
                    Quaternion.Angle(lastRecorded.rotation, rotation) < rotationCorrectionThreshold)
                {
                    // Debug.Log($"FixedUpdate for {name}: taking optimized early return instead of recording state.");
                    return;
                }
            }

            RecordState();
        }

        // manually store last recorded so we can easily check against this
        // without traversing the SortedList.
        RigidbodyState lastRecorded;
        double lastRecordTime;
        void RecordState()
        {
            // performance optimization: only call NetworkTime.time getter once
            double networkTime = NetworkTime.time;

            // instead of recording every fixedupdate, let's record in an interval.
            // we don't want to record every tiny move and correct too hard.
            if (networkTime < lastRecordTime + recordInterval) return;
            lastRecordTime = networkTime;

            // NetworkTime.time is always behind by bufferTime.
            // prediction aims to be on the exact same server time (immediately).
            // use predictedTime to record state, otherwise we would record in the past.
            double predictedTime = NetworkTime.predictedTime;

            // FixedUpdate may run twice in the same frame / NetworkTime.time.
            // for now, simply don't record if already recorded there.
            // previously we checked ContainsKey which is O(logN) for SortedList
            //   if (stateHistory.ContainsKey(predictedTime))
            //       return;
            // instead, simply store the last recorded time and don't insert if same.
            if (predictedTime == lastRecorded.timestamp) return;

            // keep state history within limit
            if (stateHistory.Count >= stateHistoryLimit)
                stateHistory.RemoveAt(0);

            // grab current position/rotation/velocity only once.
            // this is performance critical, avoid calling .transform multiple times.
            tf.GetPositionAndRotation(out Vector3 currentPosition, out Quaternion currentRotation); // faster than accessing .position + .rotation manually
#if UNITY_6000_0_OR_NEWER
            Vector3 currentVelocity = predictedRigidbody.linearVelocity;
#else
            Vector3 currentVelocity = predictedRigidbody.velocity;
#endif
            Vector3 currentAngularVelocity = predictedRigidbody.angularVelocity;

            // calculate delta to previous state (if any)
            Vector3 positionDelta = Vector3.zero;
            Vector3 velocityDelta = Vector3.zero;
            Vector3 angularVelocityDelta = Vector3.zero;
            Quaternion rotationDelta = Quaternion.identity;
            int stateHistoryCount = stateHistory.Count; // perf: only grab .Count once
            if (stateHistoryCount > 0)
            {
                RigidbodyState last = stateHistory.Values[stateHistoryCount - 1];
                positionDelta = currentPosition - last.position;
                velocityDelta = currentVelocity - last.velocity;
                // Quaternions always need to be normalized in order to be valid rotations after operations
                rotationDelta = (currentRotation * Quaternion.Inverse(last.rotation)).normalized;
                angularVelocityDelta = currentAngularVelocity - last.angularVelocity;

                // debug draw the recorded state
                // Debug.DrawLine(last.position, currentPosition, Color.red, lineTime);
            }

            // create state to insert
            RigidbodyState state = new RigidbodyState(
                predictedTime,
                positionDelta,
                currentPosition,
                rotationDelta,
                currentRotation,
                velocityDelta,
                currentVelocity,
                angularVelocityDelta,
                currentAngularVelocity
            );

            // add state to history
            stateHistory.Add(predictedTime, state);

            // manually remember last inserted state for faster .Last comparisons
            lastRecorded = state;
        }

        // optional user callbacks, in case people need to know about events.
        protected virtual void OnSnappedIntoPlace() {}
        protected virtual void OnBeforeApplyState() {}
        protected virtual void OnCorrected() {}
        protected virtual void OnBeginPrediction() {} // when the Rigidbody moved above threshold and we created a ghost
        protected virtual void OnEndPrediction() {}   // when the Rigidbody came to rest and we destroyed the ghost

        void ApplyState(double timestamp, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            // fix rigidbodies seemingly dancing in place instead of coming to rest.
            // hard snap to the position below a threshold velocity.
            // this is fine because the visual object still smoothly interpolates to it.
            // => consider both velocity and angular velocity (in case of Rigidbodies only rotating with joints etc.)
#if UNITY_6000_0_OR_NEWER
            if (predictedRigidbody.linearVelocity.magnitude <= snapThreshold &&
                predictedRigidbody.angularVelocity.magnitude <= snapThreshold)
#else
                if (predictedRigidbody.velocity.magnitude <= snapThreshold &&
                predictedRigidbody.angularVelocity.magnitude <= snapThreshold)
#endif
            {
                // Debug.Log($"Prediction: snapped {name} into place because velocity {predictedRigidbody.velocity.magnitude:F3} <= {snapThreshold:F3}");

                // apply server state immediately.
                // important to apply velocity as well, instead of Vector3.zero.
                // in case an object is still slightly moving, we don't want it
                // to stop and start moving again on client - slide as well here.
                predictedRigidbody.position = position;
                predictedRigidbody.rotation = rotation;
                // projects may keep Rigidbodies as kinematic sometimes. in that case, setting velocity would log an error
                if (!predictedRigidbody.isKinematic)
                {
#if UNITY_6000_0_OR_NEWER
                    predictedRigidbody.linearVelocity = velocity;
#else
                    predictedRigidbody.velocity = velocity;
#endif
                    predictedRigidbody.angularVelocity = angularVelocity;
                }

                // clear history and insert the exact state we just applied.
                // this makes future corrections more accurate.
                stateHistory.Clear();
                stateHistory.Add(timestamp, new RigidbodyState(
                    timestamp,
                    Vector3.zero,
                    position,
                    Quaternion.identity,
                    rotation,
                    Vector3.zero,
                    velocity,
                    Vector3.zero,
                    angularVelocity
                ));

                // user callback
                OnSnappedIntoPlace();
                return;
            }

            // we have a callback for snapping into place (above).
            // we also need one for corrections without snapping into place.
            // call it before applying pos/rot/vel in case we need to set kinematic etc.
            OnBeforeApplyState();

            // apply the state to the Rigidbody
            if (mode == PredictionMode.Smooth)
            {
                // Smooth mode separates Physics from Renderering.
                // Rendering smoothly follows Physics in SmoothFollowPhysicsCopy().
                // this allows us to be able to hard teleport to the correction.
                // which gives most accurate results since the Rigidbody can't
                // be stopped by another object when trying to correct.
                predictedRigidbody.position = position;
                predictedRigidbody.rotation = rotation;
            }
            else if (mode == PredictionMode.Fast)
            {
                // Fast mode doesn't separate physics from rendering.
                // The only smoothing we get is from Rigidbody.MovePosition.
                predictedRigidbody.MovePosition(position);
                predictedRigidbody.MoveRotation(rotation);
            }

            // there's only one way to set velocity.
            // (projects may keep Rigidbodies as kinematic sometimes. in that case, setting velocity would log an error)
            if (!predictedRigidbody.isKinematic)
            {
#if UNITY_6000_0_OR_NEWER
                predictedRigidbody.linearVelocity = velocity;
#else
                predictedRigidbody.velocity = velocity;
#endif
                predictedRigidbody.angularVelocity = angularVelocity;
            }
        }

        // process a received server state.
        // compares it against our history and applies corrections if needed.
        void OnReceivedState(double timestamp, RigidbodyState state)//, bool sleeping)
        {
            // always update remote state ghost
            if (remoteCopy != null)
            {
                Transform remoteCopyTransform = remoteCopy.transform;
                remoteCopyTransform.SetPositionAndRotation(state.position, state.rotation); // faster than .position + .rotation setters
                remoteCopyTransform.localScale = tf.lossyScale; // world scale! see CreateGhosts comment.
            }


            // DO NOT SYNC SLEEPING! this cuts benchmark performance in half(!!!)
            // color code remote sleeping objects to debug objects coming to rest
            // if (showRemoteSleeping)
            // {
            //     rend.material.color = sleeping ? Color.gray : originalColor;
            // }

            // performance: get Rigidbody position & rotation only once,
            // and together via its transform
            predictedRigidbodyTransform.GetPositionAndRotation(out Vector3 physicsPosition, out Quaternion physicsRotation);

            // OPTIONAL performance optimization when comparing idle objects.
            // even idle objects will have a history of ~32 entries.
            // sampling & traversing through them is unnecessarily costly.
            // instead, compare directly against the current rigidbody position!
            // => this is technically not 100% correct if an object runs in
            //    circles where it may revisit the same position twice.
            // => but practically, objects that didn't move will have their
            //    whole history look like the last inserted state.
            // => comparing against that is free and gives us a significant
            //    performance saving vs. a tiny chance of incorrect results due
            //    to objects running in circles.
            // => the RecordState() call below is expensive too, so we want to
            //    do this before even recording the latest state. the only way
            //    to do this (in case last recorded state is too old), is to
            //    compare against live rigidbody.position without any recording.
            //    this is as fast as it gets for skipping idle objects.
            //
            // if this ever causes issues, feel free to disable it.
            float positionToStateDistanceSqr = Vector3.SqrMagnitude(state.position - physicsPosition);
            if (compareLastFirst &&
                // Vector3.Distance(state.position, physicsPosition) < positionCorrectionThreshold && // slow comparison
                positionToStateDistanceSqr < positionCorrectionThresholdSqr &&                               // fast comparison
                Quaternion.Angle(state.rotation, physicsRotation) < rotationCorrectionThreshold)
            {
                // Debug.Log($"OnReceivedState for {name}: taking optimized early return!");
                return;
            }

            // we only capture state every 'interval' milliseconds.
            // so the newest entry in 'history' may be up to 'interval' behind 'now'.
            // if there's no latency, we may receive a server state for 'now'.
            // sampling would fail, if we haven't recorded anything in a while.
            // to solve this, always record the current state when receiving a server state.
            RecordState();

            // correction requires at least 2 existing states for 'before' and 'after'.
            // if we don't have two yet, drop this state and try again next time once we recorded more.
            if (stateHistory.Count < 2) return;

            RigidbodyState oldest = stateHistory.Values[0];
            RigidbodyState newest = stateHistory.Values[stateHistory.Count - 1];

            // edge case: is the state older than the oldest state in history?
            // this can happen if the client gets so far behind the server
            // that it doesn't have a recored history to sample from.
            // in that case, we should hard correct the client.
            // otherwise it could be out of sync as long as it's too far behind.
            if (state.timestamp < oldest.timestamp)
            {
                // when starting, client may only have 2-3 states in history.
                // it's expected that server states would be behind those 2-3.
                // only show a warning if it's behind the full history limit!
                if (stateHistory.Count >= stateHistoryLimit)
                    Debug.LogWarning($"Hard correcting client object {name} because the client is too far behind the server. History of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This would cause the client to be out of sync as long as it's behind.");

                // force apply the state
                ApplyState(state.timestamp, state.position, state.rotation, state.velocity, state.angularVelocity);
                return;
            }

            // edge case: is it newer than the newest state in history?
            // this can happen if client's predictedTime predicts too far ahead of the server.
            // in that case, log a warning for now but still apply the correction.
            // otherwise it could be out of sync as long as it's too far ahead.
            //
            // for example, when running prediction on the same machine with near zero latency.
            // when applying corrections here, this looks just fine on the local machine.
            if (newest.timestamp < state.timestamp)
            {
                // the correction is for a state in the future.
                // we clamp it to 'now'.
                // but only correct if off by threshold.
                // TODO maybe we should interpolate this back to 'now'?
                // if (Vector3.Distance(state.position, physicsPosition) >= positionCorrectionThreshold) // slow comparison
                if (positionToStateDistanceSqr >= positionCorrectionThresholdSqr) // fast comparison
                {
                    // this can happen a lot when latency is ~0. logging all the time allocates too much and is too slow.
                    // double ahead = state.timestamp - newest.timestamp;
                    // Debug.Log($"Hard correction because the client is ahead of the server by {(ahead*1000):F1}ms. History of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This can happen when latency is near zero, and is fine unless it shows jitter.");
                    ApplyState(state.timestamp, state.position, state.rotation, state.velocity, state.angularVelocity);
                }
                return;
            }

            // find the two closest client states between timestamp
            if (!Prediction.Sample(stateHistory, timestamp, out RigidbodyState before, out RigidbodyState after, out int afterIndex, out double t))
            {
                // something went very wrong. sampling should've worked.
                // hard correct to recover the error.
                Debug.LogError($"Failed to sample history of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This should never happen because the timestamp is within history.");
                ApplyState(state.timestamp, state.position, state.rotation, state.velocity, state.angularVelocity);
                return;
            }

            // interpolate between them to get the best approximation
            RigidbodyState interpolated = RigidbodyState.Interpolate(before, after, (float)t);

            // calculate the difference between where we were and where we should be
            // TODO only position for now. consider rotation etc. too later
            // float positionToInterpolatedDistance = Vector3.Distance(state.position, interpolated.position); // slow comparison
            float positionToInterpolatedDistanceSqr = Vector3.SqrMagnitude(state.position - interpolated.position); // fast comparison
            float rotationToInterpolatedDistance = Quaternion.Angle(state.rotation, interpolated.rotation);
            // Debug.Log($"Sampled history of size={stateHistory.Count} @ {timestamp:F3}: client={interpolated.position} server={state.position} difference={difference:F3} / {correctionThreshold:F3}");

            // too far off? then correct it
            if (positionToInterpolatedDistanceSqr >= positionCorrectionThresholdSqr || // fast comparison
                //positionToInterpolatedDistance >= positionCorrectionThreshold ||     // slow comparison
                rotationToInterpolatedDistance >= rotationCorrectionThreshold)
            {
                // Debug.Log($"CORRECTION NEEDED FOR {name} @ {timestamp:F3}: client={interpolated.position} server={state.position} difference={difference:F3}");

                // show the received correction position + velocity for debugging.
                // helps to compare with the interpolated/applied correction locally.
                //Debug.DrawLine(state.position, state.position + state.velocity * 0.1f, Color.white, lineTime);

                // insert the correction and correct the history on top of it.
                // returns the final recomputed state after rewinding.
                RigidbodyState recomputed = Prediction.CorrectHistory(stateHistory, stateHistoryLimit, state, before, after, afterIndex);

                // log, draw & apply the final position.
                // always do this here, not when iterating above, in case we aren't iterating.
                // for example, on same machine with near zero latency.
                // int correctedAmount = stateHistory.Count - afterIndex;
                // Debug.Log($"Correcting {name}: {correctedAmount} / {stateHistory.Count} states to final position from: {rb.position} to: {last.position}");
                //Debug.DrawLine(physicsCopyRigidbody.position, recomputed.position, Color.green, lineTime);
                ApplyState(recomputed.timestamp, recomputed.position, recomputed.rotation, recomputed.velocity, recomputed.angularVelocity);

                // user callback
                OnCorrected();
            }
        }

        // send state to clients every sendInterval.
        // reliable for now.
        // TODO we should use the one from FixedUpdate
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            // Time.time was at the beginning of this frame.
            // NetworkLateUpdate->Broadcast->OnSerialize is at the end of the frame.
            // as result, client should use this to correct the _next_ frame.
            // otherwise we see noticeable resets that seem off by one frame.
            //
            // to solve this, we can send the current deltaTime.
            // server is technically supposed to be at a fixed frame rate, but this can vary.
            // sending server's current deltaTime is the safest option.
            // client then applies it on top of remoteTimestamp.


            // FAST VERSION: this shows in profiler a lot, so cache EVERYTHING!
            tf.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);  // faster than tf.position + tf.rotation. server's rigidbody is on the same transform.

            // simple but slow write:
            // writer.WriteFloat(Time.deltaTime);
            // writer.WriteVector3(position);
            // writer.WriteQuaternion(rotation);
            // writer.WriteVector3(predictedRigidbody.velocity);
            // writer.WriteVector3(predictedRigidbody.angularVelocity);

            // performance optimization: write a whole struct at once via blittable:
            PredictedSyncData data = new PredictedSyncData(
                Time.deltaTime,
                position,
                rotation,
#if UNITY_6000_0_OR_NEWER
                predictedRigidbody.linearVelocity,
#else
                predictedRigidbody.velocity,
#endif
                predictedRigidbody.angularVelocity);//,
                // DO NOT SYNC SLEEPING! this cuts benchmark performance in half(!!!)
                // predictedRigidbody.IsSleeping());
            writer.WritePredictedSyncData(data);
        }

        // read the server's state, compare with client state & correct if necessary.
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // deserialize data
            // we want to know the time on the server when this was sent, which is remoteTimestamp.
            double timestamp = NetworkClient.connection.remoteTimeStamp;

            // simple but slow read:
            // double serverDeltaTime = reader.ReadFloat();
            // Vector3 position        = reader.ReadVector3();
            // Quaternion rotation     = reader.ReadQuaternion();
            // Vector3 velocity        = reader.ReadVector3();
            // Vector3 angularVelocity = reader.ReadVector3();

            // performance optimization: read a whole struct at once via blittable:
            PredictedSyncData data = reader.ReadPredictedSyncData();
            double serverDeltaTime = data.deltaTime;
            Vector3 position = data.position;
            Quaternion rotation = data.rotation;
            Vector3 velocity = data.velocity;
            Vector3 angularVelocity = data.angularVelocity;
            // DO NOT SYNC SLEEPING! this cuts benchmark performance in half(!!!)
            // bool sleeping = data.sleeping != 0;

            // server sends state at the end of the frame.
            // parse and apply the server's delta time to our timestamp.
            // otherwise we see noticeable resets that seem off by one frame.
            timestamp += serverDeltaTime;

            // however, adding yet one more frame delay gives much(!) better results.
            // we don't know why yet, so keep this as an option for now.
            // possibly because client captures at the beginning of the frame,
            // with physics happening at the end of the frame?
            if (oneFrameAhead) timestamp += serverDeltaTime;

            // process received state
            OnReceivedState(timestamp, new RigidbodyState(timestamp, Vector3.zero, position, Quaternion.identity, rotation, Vector3.zero, velocity, Vector3.zero, angularVelocity));//, sleeping);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            // force syncDirection to be ServerToClient
            syncDirection = SyncDirection.ServerToClient;

            // state should be synced immediately for now.
            // later when we have prediction fully dialed in,
            // then we can maybe relax this a bit.
            syncInterval = 0;
        }

        // helper function for Physics tests to check if a Rigidbody belongs to
        // a PredictedRigidbody component (either on it, or on its ghost).
        public static bool IsPredicted(Rigidbody rb, out PredictedRigidbody predictedRigidbody)
        {
            // by default, Rigidbody is on the PredictedRigidbody GameObject
            if (rb.TryGetComponent(out predictedRigidbody))
                return true;

            // it might be on a ghost while interacting
            if (rb.TryGetComponent(out PredictedRigidbodyPhysicsGhost ghost))
            {
                predictedRigidbody = ghost.target.GetComponent<PredictedRigidbody>();
                return true;
            }

            // otherwise the Rigidbody does not belong to any PredictedRigidbody.
            predictedRigidbody = null;
            return false;
        }

        // helper function for Physics tests to check if a Collider (which may be in children) belongs to
        // a PredictedRigidbody component (either on it, or on its ghost).
        public static bool IsPredicted(Collider co, out PredictedRigidbody predictedRigidbody)
        {
            // by default, Collider is on the PredictedRigidbody GameObject or it's children.
            predictedRigidbody = co.GetComponentInParent<PredictedRigidbody>();
            if (predictedRigidbody != null)
                return true;

            // it might be on a ghost while interacting
            PredictedRigidbodyPhysicsGhost ghost = co.GetComponentInParent<PredictedRigidbodyPhysicsGhost>();
            if (ghost != null && ghost.target != null && ghost.target.TryGetComponent(out predictedRigidbody))
                return true;

            // otherwise the Rigidbody does not belong to any PredictedRigidbody.
            predictedRigidbody = null;
            return false;
        }
    }
}
