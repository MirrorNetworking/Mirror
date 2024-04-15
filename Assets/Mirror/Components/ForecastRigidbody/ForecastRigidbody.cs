// Forecasting predicts for a few frames and then smooth follows server state
// without any physics overhead, just like NetworkTransform.
//
// This is for scenes with extreme amounts of physics which can't be simulated
// on low end client machines.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    enum ForecastState
    {
        PREDICTING, // 100% client side physics prediction
        BLENDING,   // blending client prediction with server state
        FOLLOWING,  // 100% server sided physics, client only follows .transform
    }

    [RequireComponent(typeof(Rigidbody))]
    public class ForecastRigidbody : NetworkBehaviour
    {
        Transform tf; // this component is performance critical. cache .transform getter!
        Renderer rend;
        Collider col;
        float initialSyncInterval;

        // Prediction sometimes moves the Rigidbody to a ghost object.
        // .predictedRigidbody is always kept up to date to wherever the RB is.
        // other components should use this when accessing Rigidbody.
        public Rigidbody predictedRigidbody;
        Transform predictedRigidbodyTransform; // predictedRigidbody.transform for performance (Get/SetPositionAndRotation)

        Vector3 lastPosition;

        [Header("Blending")]
        [Tooltip("Blend to remote state over a N * rtt time.\n  For a 200ms ping, we blend over N * 200ms.\n  For 20ms ping, we blend over N * 20 ms.")]
        public float blendingRttMultiplier = 2;
        public float blendingTime => (float)NetworkTime.rtt * blendingRttMultiplier;
        [Tooltip("Blending speed over time from 0 to 1. Exponential is recommended.")]
        public AnimationCurve blendingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        ForecastState state = ForecastState.FOLLOWING; // follow until the player interacts
        double predictionStartTime;

        [Header("Dampening")]
        [Tooltip("Locally applied force is slowed down a bit compared to the server force, to make catch up more smooth.")]
        [Range(0.05f, 1)] public float localForceDampening = 0.2f; // 50% is too obvious

        [Header("Smoothing")]
        public readonly SortedList<double, TransformSnapshot> clientSnapshots = new SortedList<double, TransformSnapshot>(16);

        [Header("Collision Chaining")]
        [Tooltip("Enable to have actively predicted Rigidbodies activate other Rigidbodies they collide with.")]
        public bool collisionChaining = true;
        [Tooltip("If a player interacts with an object, it can recursively activate other objects it collides with.\nDepth is the chain link from A->B->C->etc., it doesn't mean the amount of A->B, A->C, etc.\nNeeds to be finite to avoid chain activations going on forever like A->B->C->A (easy to notice in the stacked prediction example.")]
        public int maxCollisionChainingDepth = 2; // A->B->C is enough!
        int remainingCollisionChainDepth;

        // motion smoothing happen on-demand, because it requires moving physics components to another GameObject.
        // this only starts at a given velocity and ends when stopped moving.
        // to avoid constant on/off/on effects, it also stays on for a minimum time.
        [Header("Sleeping")]
        [Tooltip("Low send rate until velocity is above this threshold.")]
        public float velocitySensitivity = 0.1f;
        float velocitySensitivitySqr; // ² cached in Awake
        [Tooltip("Low send rate until angular velocity is above this threshold.")]
        public float angularVelocitySensitivity = 5.0f; // Billiards demo: 0.1 is way too small, takes forever for IsMoving()==false
        float angularVelocitySensitivitySqr; // ² cached in Awake

        [Tooltip("Applying server corrections one frame ahead gives much better results. We don't know why yet, so this is an option for now.")]
        public bool oneFrameAhead = true;

        [Header("Bandwidth")]
        [Tooltip("Reduce sends while velocity==0. Client's objects may slightly move due to gravity/physics, so we still want to send corrections occasionally even if an object is idle on the server the whole time.")]
        public bool reduceSendsWhileIdle = true;

#if UNITY_EDITOR // PERF: only access .material in Editor, as it may instantiate!
        [Header("Debugging")]
        public bool debugColors = false;
        Color originalColor = Color.white;
        public Color predictingColor = Color.green;
        public Color blendingColor = Color.yellow; // when actually interpolating towards a blend in front of us
#endif

        protected virtual void Awake()
        {
            tf = transform;
            rend = GetComponentInChildren<Renderer>();
            predictedRigidbody = GetComponent<Rigidbody>();
            col = GetComponentInChildren<Collider>();
            if (predictedRigidbody == null) throw new InvalidOperationException($"Prediction: {name} is missing a Rigidbody component.");
            predictedRigidbodyTransform = predictedRigidbody.transform;

            initialSyncInterval = syncInterval;

            // in fast mode, we need to force enable Rigidbody.interpolation.
            // otherwise there's not going to be any smoothing whatsoever.
            // PERF: disable this for now!
            // predictedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            // cache computations
            velocitySensitivitySqr = velocitySensitivity * velocitySensitivity;
            angularVelocitySensitivitySqr = angularVelocitySensitivity * angularVelocitySensitivity;

            // save renderer color
            // note if objects share a material, accessing ".material" will
            // instantiate one which can be a massive performance overhead.
            // only use debug colors when debugging!
#if UNITY_EDITOR // PERF: only access .material in Editor, as it may instantiate!
            if (debugColors) originalColor = rend.material.color;
#endif
        }

        public override void OnStartClient()
        {
            // set Rigidbody as kinematic by default on clients.
            // it's only dynamic on the server, and while predicting on clients.
            predictedRigidbody.isKinematic = true;
        }

        void AddPredictedForceInternal(Vector3 force, ForceMode mode)
        {
            // apply local force dampening.
            // client applies a bit less force than the server, so that
            // catching up to blending state will be easier.
            // for example: dampening = 0.1 means subtract 10% force
            force *= (1 - localForceDampening);

            // explicitly start predicting physics
            BeginPredicting();
            predictedRigidbody.AddForce(force, mode);
        }

        // client prediction API
        public void AddPredictedForce(Vector3 force, ForceMode mode)
        {
            // player interacted with this object explicitly.
            // restart the collision chain at max.
            remainingCollisionChainDepth = maxCollisionChainingDepth;

            // add the predicted force
            AddPredictedForceInternal(force, mode);
        }

        void AddPredictedForceChain(Vector3 force, ForceMode mode, int newChainDepth)
        {
            // apply the collision chain depth
            remainingCollisionChainDepth = newChainDepth;

            // add the predicted force
            AddPredictedForceInternal(force, mode);
        }

        public void AddPredictedForceAtPosition(Vector3 force, Vector3 position, ForceMode mode)
        {
            // player interacted with this object explicitly.
            // restart the collision chain at max.
            remainingCollisionChainDepth = maxCollisionChainingDepth;

            // apply local force dampening.
            // client applies a bit less force than the server, so that
            // catching up to blending state will be easier.
            // for example: dampening = 0.1 means subtract 10% force
            force *= (1 - localForceDampening);

            // explicitly start predicting physics
            BeginPredicting();
            predictedRigidbody.AddForceAtPosition(force, position, mode);
        }

        protected void BeginPredicting()
        {
            predictedRigidbody.isKinematic = false; // full physics sync
            state = ForecastState.PREDICTING;
#if UNITY_EDITOR // PERF: only access .material in Editor, as it may instantiate!
            if (debugColors) rend.material.color = predictingColor;
#endif
            // we want to predict until the first server state for our [Command] AddForce came in.
            // we know the time when our [Command] arrives on server: NetworkTime.predictedTime.
            predictionStartTime = NetworkTime.predictedTime; // !!! not .time !!!

            OnBeginPrediction();
            // Debug.Log($"{name} BEGIN PREDICTING @ {predictionStartTime:F2}");
        }

        double blendingStartTime;
        protected void BeginBlending()
        {
            state = ForecastState.BLENDING;
            // if (debugColors) rend.material.color = blendingAheadColor; set in update depending on ahead/behind
            blendingStartTime = NetworkTime.localTime;
            OnBeginBlending();

            // clear any previous snapshots from teleports, old states, while prediction etc.
            // start building a buffer while blending, to later follow while FOLLOWING.
            clientSnapshots.Clear();
            // Debug.Log($"{name} BEGIN BLENDING");
        }

        protected void BeginFollowing()
        {
            predictedRigidbody.isKinematic = true; // full transform sync
            state = ForecastState.FOLLOWING;
#if UNITY_EDITOR // PERF: only access .material in Editor, as it may instantiate!
            if (debugColors) rend.material.color = originalColor;
#endif
            // reset the collision chain depth so it starts at 0 again next time
            remainingCollisionChainDepth = 0;
            OnBeginFollow();
            // Debug.Log($"{name} BEGIN FOLLOW");
        }

        void UpdateServer()
        {
            // bandwidth optimization while idle.
            if (reduceSendsWhileIdle)
            {
                // while moving, always sync every syncInterval..
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
                syncInterval = IsMoving() ? initialSyncInterval : 1;
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
            predictedRigidbody.velocity.sqrMagnitude >= velocitySensitivitySqr ||
            predictedRigidbody.angularVelocity.sqrMagnitude >= angularVelocitySensitivitySqr;

        void Update()
        {
            if (isServer) UpdateServer();
        }

        // Prediction always has a Rigidbody.
        // position changes should always happen in FixedUpdate, even while kinematic.
        // improves benchmark performance from 600 -> 870 FPS.
        void FixedUpdateClient()
        {
            // PREDICTING checks state, which happens in update()
            if (state == ForecastState.PREDICTING)
            {
                // we want to predict until the first server state came in.
                // -> our [Command] AddForce is sent locally.
                // -> predictionStartTime was set to NetworkTime.predictedTime,
                //    which is the time on server when the [Command] will arrive.
                // -> we want to wait until the last received is at least >= start time.
                // TODO add a safety buffer on top to make sure it's the state after [Command]?
                //      but technically doesn't make a difference if it just barely moved anyway.
                if (lastReceivedState.timestamp > predictionStartTime)
                {
                    // Debug.Log($"{name} END PREDICTING because received state = {lastReceivedState.timestamp:F2} > prediction start = {predictionStartTime:F2}");
                    BeginBlending();
                }
            }
            else if (state == ForecastState.BLENDING)
            {
                // TODO snapshot interpolation

                // blend between local and remote position
                // set debug color
#if UNITY_EDITOR // PERF: only access .material in Editor, as it may instantiate!
                if (debugColors)
                {
                    rend.material.color = blendingColor;
                }
#endif

                // sample the blending curve to find out how much to blend right now

                float blendingElapsed = (float)(NetworkTime.localTime - blendingStartTime);
                float relativeElapsed = blendingElapsed / blendingTime;
                float p = blendingCurve.Evaluate(relativeElapsed);
                // Debug.Log($"{name} BLENDING @ {blendingElapsed:F2} / {blendingTime:F2} => {(p*100):F0}%");

                // blend local position to remote position.
                // getting both at once is fastest.
                tf.GetPositionAndRotation(out Vector3 currentPosition, out Quaternion currentRotation);

                // slow and simple version:
                //   float distance = Vector3.Distance(currentPosition, physicsPosition);
                //   if (distance > smoothFollowThreshold)
                // faster version
                Vector3 delta = lastReceivedState.position - currentPosition;
                float sqrDistance = Vector3.SqrMagnitude(delta);
                float distance = Mathf.Sqrt(sqrDistance);

                // smoothly interpolate to the target position.
                float positionStep = distance * p;

                Vector3 newPosition = PredictedRigidbody.MoveTowardsCustom(
                    currentPosition,
                    lastReceivedState.position,
                    delta,
                    sqrDistance,
                    distance,
                    positionStep);

                // smoothly interpolate to the target rotation.
                // Quaternion.RotateTowards doesn't seem to work at all, so let's use SLerp.
                // Quaternions always need to be normalized in order to be a valid rotation after operations
                Quaternion newRotation = Quaternion.Slerp(currentRotation, lastReceivedState.rotation, p).normalized;

                // assign position and rotation together. faster than accessing manually.
                // in theory we must always set rigidbody.position/rotation instead of transform:
                // https://forum.unity.com/threads/how-expensive-is-physics-synctransforms.1366146/#post-9557491
                // however, tf.SetPositionAndRotation is faster in our Prediction Benchmark.
                predictedRigidbody.position = newPosition;
                predictedRigidbody.rotation = newRotation;
                // tf.SetPositionAndRotation(newPosition, newRotation);

                // transition to FOLLOWING after blending is done.
                // we could check 'if p >= 1' but if the user's curve never
                // reaches a value of '1' then we would never transition.
                // best to reach if elapsed time > blend time.
                if (blendingElapsed >= blendingTime)
                {
                    // Debug.Log($"{name} END BLENDING");
                    BeginFollowing();
                }
            }
            // FOLLOWING sets Transform, which happens in Update().
            else if (state == ForecastState.FOLLOWING)
            {
                // hard set position & rotation.
                // in theory we must always set rigidbody.position/rotation instead of transform:
                // https://forum.unity.com/threads/how-expensive-is-physics-synctransforms.1366146/#post-9557491
                // however, tf.SetPositionAndRotation is faster in our Prediction Benchmark.
                //   predictedRigidbody.position = lastReceivedState.position;
                //   predictedRigidbody.rotation = lastReceivedState.rotation;
                // tf.SetPositionAndRotation(lastReceivedState.position, lastReceivedState.rotation);

                // only while we have snapshots
                if (clientSnapshots.Count > 0)
                {
                    // step the interpolation without touching time.
                    // NetworkClient is responsible for time globally.
                    SnapshotInterpolation.StepInterpolation(
                        clientSnapshots,
                        NetworkTime.time, // == NetworkClient.localTimeline from snapshot interpolation
                        out TransformSnapshot from,
                        out TransformSnapshot to,
                        out double t);

                    // interpolate & apply
                    TransformSnapshot computed = TransformSnapshot.Interpolate(from, to, t);
                    // tf.SetPositionAndRotation(computed.position, computed.rotation); // scale is ignored
                    predictedRigidbody.position = computed.position;
                    predictedRigidbody.rotation = computed.rotation;
                }
            }
        }

        void FixedUpdate()
        {
            if (isClientOnly) FixedUpdateClient();
        }

#if UNITY_EDITOR // PERF: only run gizmos in editor
        void OnDrawGizmos()
        {
            // draw server state while blending
            if (state == ForecastState.BLENDING)
            {
                Gizmos.color = blendingColor;
                Gizmos.DrawWireCube(lastReceivedState.position, col.bounds.size);
            }
        }
#endif

        // while predicting on client, if we hit another object then we need to
        // start predicting this one too.
        // otherwise the collision response would be delayed until next server
        // state to follow comes in.
        [ClientCallback]
        void OnCollisionEnter(Collision collision)
        {
            // is collision chaining enabled?
            if (!collisionChaining) return;

            // if we are FOLLOWING, then there's nothing to do.
            if (state == ForecastState.FOLLOWING) return;

            // is the other object a ForecastRigidbody?
            if (!collision.collider.TryGetComponent(out ForecastRigidbody other)) return;
            // Debug.Log($"{name} @ {state} collided with {other.name} @ {other.state}");

            // is the other object already predicting? then don't call events again.
            if (other.state != ForecastState.FOLLOWING) return;

            // collided with an object that has not yet been activate.
            // should this object still activate other objects?
            // for example, chain depth = 3 means: A->B->C.
            // so C->D would not activate anymore.
            // (we always need to check this only for inactivate objects,
            //  otherwise A->B->A->B etc. would reduce the chain depth forever).
            if (remainingCollisionChainDepth <= 0) return;

            // the other object is in FOLLOWING mode (kinematic).
            // PhysX will register the collision, but not add the collision force while kinematic.
            // we need to add the force manually, which will also begin predicting it.
            // => collision.impulse is sometimes from A->B and sometimes B->A.
            // => we need to calculate the direction manually to always be A->B.
            Vector3 direction = other.transform.position - transform.position;
            Vector3 impulse = direction.normalized * collision.impulse.magnitude;


            other.AddPredictedForceChain(impulse, ForceMode.Impulse, remainingCollisionChainDepth - 1);
        }

        // optional user callbacks, in case people need to know about events.
        protected virtual void OnSnappedIntoPlace() {}
        protected virtual void OnBeforeApplyState() {}
        protected virtual void OnBeginPrediction() {}
        protected virtual void OnBeginBlending() {}
        protected virtual void OnBeginFollow() {}

        // process a received server state.
        // compares it against our history and applies corrections if needed.
        ForecastRigidbodyState lastReceivedState;
        void OnReceivedState(ForecastRigidbodyState data)//, bool sleeping)
        {
            // store last time
            lastReceivedState = data;

            // add to snapshot interpolation for smooth following.
            // add a small timeline offset to account for decoupled arrival of
            // NetworkTime and NetworkTransform snapshots.
            // needs to be sendInterval. half sendInterval doesn't solve it.
            // https://github.com/MirrorNetworking/Mirror/issues/3427
            // remove this after LocalWorldState.

            // insert transform transform snapshot.
            // ignore while predicting since they'll be from old server state.
            if (state != ForecastState.PREDICTING)
            {
                SnapshotInterpolation.InsertIfNotExists(
                    clientSnapshots,
                    NetworkClient.snapshotSettings.bufferLimit,
                    new TransformSnapshot(
                        NetworkClient.connection.remoteTimeStamp, // TODO use Ninja's offset from NT-R?: + timeStampAdjustment + offset, // arrival remote timestamp. NOT remote time.
                        NetworkTime.localTime, // Unity 2019 doesn't have timeAsDouble yet
                        data.position,
                        data.rotation,
                        Vector3.zero // scale is unused
                    )
                );
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
            ForecastSyncData data = new ForecastSyncData(
                Time.deltaTime,
                position,
                rotation);
            writer.WriteForecastSyncData(data);
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
            ForecastSyncData data = reader.ReadForecastSyncData();
            double serverDeltaTime = data.deltaTime;
            Vector3 position = data.position;
            Quaternion rotation = data.rotation;

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
            OnReceivedState(new ForecastRigidbodyState(timestamp, position, rotation));
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            // force syncDirection to be ServerToClient
            syncDirection = SyncDirection.ServerToClient;
        }

        // helper function for Physics tests to check if a Rigidbody belongs to
        // a ForecastRigidbody component (either on it, or on its ghost).
        public static bool IsPredicted(Rigidbody rb, out ForecastRigidbody predictedRigidbody)
        {
            // by default, Rigidbody is on the ForecastRigidbody GameObject
            if (rb.TryGetComponent(out predictedRigidbody))
                return true;

            // otherwise the Rigidbody does not belong to any ForecastRigidbody.
            predictedRigidbody = null;
            return false;
        }

        // helper function for Physics tests to check if a Collider (which may be in children) belongs to
        // a ForecastRigidbody component (either on it, or on its ghost).
        public static bool IsPredicted(Collider co, out ForecastRigidbody predictedRigidbody)
        {
            // by default, Collider is on the ForecastRigidbody GameObject or it's children.
            predictedRigidbody = co.GetComponentInParent<ForecastRigidbody>();
            if (predictedRigidbody != null)
                return true;

            // otherwise the Rigidbody does not belong to any ForecastRigidbody.
            predictedRigidbody = null;
            return false;
        }
    }
}
