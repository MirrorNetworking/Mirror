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

        // Prediction sometimes moves the Rigidbody to a ghost object.
        // .predictedRigidbody is always kept up to date to wherever the RB is.
        // other components should use this when accessing Rigidbody.
        public Rigidbody predictedRigidbody;
        Transform predictedRigidbodyTransform; // predictedRigidbody.transform for performance (Get/SetPositionAndRotation)

        Vector3 lastPosition;

        [Header("Blending")]
        [Tooltip("Blend to remote state over a N * rtt time.\n  For a 200ms ping, we blend over N * 200ms.\n  For 20ms ping, we blend over N * 20 ms.")]
        public float blendingRttMultiplier = 3;
        public float blendingTime => (float)NetworkTime.rtt * blendingRttMultiplier;
        [Tooltip("Blending speed over time from 0 to 1. Exponential is recommended.")]
        public AnimationCurve blendingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        ForecastState state = ForecastState.FOLLOWING; // follow until the player interacts
        double predictionStartTime;

        [Header("Dampening")]
        [Tooltip("Locally applied force is slowed down a bit compared to the server force, to make catch up more smooth.")]
        [Range(0.05f, 1)] public float localForceDampening = 0.2f; // never 0 to ensure blending ALWAYS catches up instead of same speed

        // motion smoothing happen on-demand, because it requires moving physics components to another GameObject.
        // this only starts at a given velocity and ends when stopped moving.
        // to avoid constant on/off/on effects, it also stays on for a minimum time.
        [Header("Motion Smoothing")]
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

        [Header("Reconciliation")]
        [Tooltip("Correction threshold in meters. For example, 0.1 means that if the client is off by more than 10cm, it gets corrected.")]
        public double positionCorrectionThreshold = 0.10;
        double positionCorrectionThresholdSqr; // ² cached in Awake
        [Tooltip("Correction threshold in degrees. For example, 5 means that if the client is off by more than 5 degrees, it gets corrected.")]
        public double rotationCorrectionThreshold = 5;

        [Tooltip("Applying server corrections one frame ahead gives much better results. We don't know why yet, so this is an option for now.")]
        public bool oneFrameAhead = true;

        [Header("Bandwidth")]
        [Tooltip("Reduce sends while velocity==0. Client's objects may slightly move due to gravity/physics, so we still want to send corrections occasionally even if an object is idle on the server the whole time.")]
        public bool reduceSendsWhileIdle = true;

        [Header("Debugging")]
        public bool debugColors = false;
        Color originalColor = Color.white;
        public Color predictingColor = Color.green;
        public Color blendingColor = Color.yellow; // when actually interpolating towards a blend in front of us

        protected virtual void Awake()
        {
            tf = transform;
            rend = GetComponentInChildren<Renderer>();
            predictedRigidbody = GetComponent<Rigidbody>();
            col = GetComponentInChildren<Collider>();
            if (predictedRigidbody == null) throw new InvalidOperationException($"Prediction: {name} is missing a Rigidbody component.");
            predictedRigidbodyTransform = predictedRigidbody.transform;


            // in fast mode, we need to force enable Rigidbody.interpolation.
            // otherwise there's not going to be any smoothing whatsoever.
            predictedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            // cache computations
            motionSmoothingVelocityThresholdSqr = motionSmoothingVelocityThreshold * motionSmoothingVelocityThreshold;
            motionSmoothingAngularVelocityThresholdSqr = motionSmoothingAngularVelocityThreshold * motionSmoothingAngularVelocityThreshold;
            positionCorrectionThresholdSqr = positionCorrectionThreshold * positionCorrectionThreshold;

            // save renderer color
            originalColor = rend.material.color;
        }

        public override void OnStartClient()
        {
            // set Rigidbody as kinematic by default on clients.
            // it's only dynamic on the server, and while predicting on clients.
            predictedRigidbody.isKinematic = true;
        }

        // client prediction API
        public void AddPredictedForce(Vector3 force, ForceMode mode)
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

        protected void BeginPredicting()
        {
            predictedRigidbody.isKinematic = false; // full physics sync
            state = ForecastState.PREDICTING;
            if (debugColors) rend.material.color = predictingColor;
            // we want to predict until the first server state for our [Command] AddForce came in.
            // we know the time when our [Command] arrives on server: NetworkTime.predictedTime.
            predictionStartTime = NetworkTime.predictedTime; // !!! not .time !!!
            OnBeginPrediction();
            Debug.Log($"{name} BEGIN PREDICTING @ {predictionStartTime:F2}");
        }

        double blendingStartTime;
        float BlendingElapsedTime() => (float)(NetworkTime.time - blendingStartTime);
        protected void BeginBlending()
        {
            state = ForecastState.BLENDING;
            // if (debugColors) rend.material.color = blendingAheadColor; set in update depending on ahead/behind
            blendingStartTime = NetworkTime.time;
            OnBeginBlending();
            Debug.Log($"{name} BEGIN BLENDING");
        }

        protected void BeginFollow()
        {
            predictedRigidbody.isKinematic = true; // full transform sync
            state = ForecastState.FOLLOWING;
            if (debugColors) rend.material.color = originalColor;
            OnBeginFollow();
            Debug.Log($"{name} BEGIN FOLLOW");
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
            predictedRigidbody.velocity.sqrMagnitude >= motionSmoothingVelocityThresholdSqr ||
            predictedRigidbody.angularVelocity.sqrMagnitude >= motionSmoothingAngularVelocityThresholdSqr;

        // check if following the remote state would move us backwards, or forward.
        // we never want to interpolate backwards.
        bool RemoteInSameDirection()
        {
            Vector3 direction = lastReceivedState.position - transform.position;

            // is this in the direction we are going, or behind us (the opposite)?
            bool opposite = Vector3.Dot(direction, predictedRigidbody.velocity) < 0;
            return !opposite;
        }

        // when using Fast mode, we don't create any ghosts.
        // but we still want to check IsMoving() in order to support the same
        // user callbacks.
        void UpdateClient()
        {
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
                    Debug.Log($"{name} END PREDICTING because received state = {lastReceivedState.timestamp:F2} > prediction start = {predictionStartTime:F2}");
                    BeginBlending();
                }
            }
            else if (state == ForecastState.BLENDING)
            {
                // BLENDING sets Rigidbody which happens in FixedUpdate()!
            }
            else if (state == ForecastState.FOLLOWING)
            {
                // hard set position & rotation.
                // TODO snapshot interpolation
                tf.SetPositionAndRotation(lastReceivedState.position, lastReceivedState.rotation);
            }
        }

        void Update()
        {
            if (isServer) UpdateServer();
            if (isClientOnly) UpdateClient();
        }

        void FixedUpdateClient()
        {
            // physics movements need to be done in FixedUpdate.

            if (state == ForecastState.PREDICTING)
            {
                // PREDICTING checks state, which happens in update()
            }
            else if (state == ForecastState.BLENDING)
            {
                // TODO snapshot interpolation

                // blend between local and remote position
                // set debug color
                if (debugColors)
                {
                    rend.material.color = blendingColor;
                }

                // sample the blending curve to find out how much to blend right now
                float blendingElapsed = BlendingElapsedTime();
                float relativeElapsed = blendingElapsed / blendingTime;
                float p = blendingCurve.Evaluate(relativeElapsed);
                Debug.Log($"{name} BLENDING @ {blendingElapsed:F2} / {blendingTime:F2} => {(p*100):F0}%");

                // blend local position to remote position
                Vector3 currentPosition = predictedRigidbody.position;
                Quaternion currentRotation = predictedRigidbody.rotation;

                // smoothly interpolate to the target position.
                // speed relative to how far away we are.
                // => speed increases by distance² because the further away, the
                //    sooner we need to catch the fuck up
                // float positionStep = (distance * distance) * interpolationSpeed;
                float distance = Vector3.Distance(currentPosition, lastReceivedState.position);
                float positionStep = distance * p;
                Vector3 newPosition = Vector3.MoveTowards(currentPosition, lastReceivedState.position, positionStep);

                // smoothly interpolate to the target rotation.
                // Quaternion.RotateTowards doesn't seem to work at all, so let's use SLerp.
                // Quaternions always need to be normalized in order to be a valid rotation after operations
                Quaternion newRotation = Quaternion.Slerp(currentRotation, lastReceivedState.rotation, p).normalized;

                // assign rigidbody position & rotation while keeping velocity to keep moving
                predictedRigidbody.MovePosition(newPosition);
                predictedRigidbody.MoveRotation(newRotation);
            }
            else if (state == ForecastState.FOLLOWING)
            {
                // FOLLOWING sets Transform, which happens in Update().
            }
        }

        void FixedUpdate()
        {
            if (isClientOnly) FixedUpdateClient();
        }

        void OnDrawGizmos()
        {
            // draw server state while blending
            if (state == ForecastState.BLENDING)
            {
                Gizmos.color = blendingColor;
                Gizmos.DrawWireCube(lastReceivedState.position, col.bounds.size);
            }
        }

        // while predicting on client, if we hit another object then we need to
        // start predicting this one too.
        // otherwise the collision response would be delayed until next server
        // state to follow comes in.
        [ClientCallback]
        void OnCollisionEnter(Collision collision)
        {
            // if we are FOLLOWING, then there's nothing to do.
            if (state == ForecastState.FOLLOWING) return;

            // is the other object a ForecastRigidbody?
            if (!collision.collider.TryGetComponent(out ForecastRigidbody other)) return;
            Debug.Log($"{name} @ {state} collided with {other.name} @ {other.state}");

            // is the other object already predicting? then don't call events again.
            if (other.state != ForecastState.FOLLOWING) return;

            // start predicting the other object too.
            other.BeginPredicting();
        }

        // optional user callbacks, in case people need to know about events.
        protected virtual void OnSnappedIntoPlace() {}
        protected virtual void OnBeforeApplyState() {}
        protected virtual void OnBeginPrediction() {}
        protected virtual void OnBeginBlending() {}
        protected virtual void OnBeginFollow() {}

        void ApplyState(double timestamp, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            /*
            // fix rigidbodies seemingly dancing in place instead of coming to rest.
            // hard snap to the position below a threshold velocity.
            // this is fine because the visual object still smoothly interpolates to it.
            // => consider both velocity and angular velocity (in case of Rigidbodies only rotating with joints etc.)
            if (predictedRigidbody.velocity.magnitude <= snapThreshold &&
                predictedRigidbody.angularVelocity.magnitude <= snapThreshold)
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
                    predictedRigidbody.velocity = velocity;
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
            */

            // we have a callback for snapping into place (above).
            // we also need one for corrections without snapping into place.
            // call it before applying pos/rot/vel in case we need to set kinematic etc.
            OnBeforeApplyState();

            // apply the state to the Rigidbody
            // Fast mode doesn't separate physics from rendering.
            // The only smoothing we get is from Rigidbody.MovePosition.
            predictedRigidbody.MovePosition(position);
            predictedRigidbody.MoveRotation(rotation);

            // there's only one way to set velocity.
            // (projects may keep Rigidbodies as kinematic sometimes. in that case, setting velocity would log an error)
            if (!predictedRigidbody.isKinematic)
            {
                predictedRigidbody.velocity = velocity;
                predictedRigidbody.angularVelocity = angularVelocity;
            }
        }

        // process a received server state.
        // compares it against our history and applies corrections if needed.
        RigidbodyState lastReceivedState;
        void OnReceivedState(double timestamp, RigidbodyState data)//, bool sleeping)
        {
            // store last time
            lastReceivedState = data;


            /*

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

                // blend the final correction towards current server state over time.
                // this is the idea of ForecastRigidbody.
                // TODO once we are at server state, let snapshot interpolation take over.
                // RigidbodyState blended = RigidbodyState.Interpolate(recomputed, state, blendPerSync);
                // Debug.DrawLine(recomputed.position, blended.position, Color.green, 10.0f);

                // log, draw & apply the final position.
                // always do this here, not when iterating above, in case we aren't iterating.
                // for example, on same machine with near zero latency.
                // int correctedAmount = stateHistory.Count - afterIndex;
                // Debug.Log($"Correcting {name}: {correctedAmount} / {stateHistory.Count} states to final position from: {rb.position} to: {last.position}");
                //Debug.DrawLine(physicsCopyRigidbody.position, recomputed.position, Color.green, lineTime);
                ApplyState(recomputed.timestamp, recomputed.position, recomputed.rotation, recomputed.velocity, recomputed.angularVelocity);

                // insert the blended state into the history.
                // this makes it permanent, instead of blending every time but rarely recording.
                RecordState();

                // user callback
                OnCorrected();
            }
            */
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
                predictedRigidbody.velocity,
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
