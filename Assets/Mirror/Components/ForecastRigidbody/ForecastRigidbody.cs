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

    struct PendingForce
    {
        public Vector3 force;
        public Vector3? position; // in case of AddForceAtPosition
        public ForceMode mode;
        public double time;
    }

    [RequireComponent(typeof(Rigidbody))]
    public class ForecastRigidbody : NetworkTransformOld // reuse NT for smooth sync for now. simplify into a lite-version later.
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
        public float blendingRttMultiplier = 3;
        public float blendingTime => (float)NetworkTime.rtt * blendingRttMultiplier;
        ForecastState state = ForecastState.FOLLOWING; // follow until the player interacts
        double predictionStartTime;
        [Tooltip("When Blending, we never want to move backwards while interpolating to server position as that would be obvious visual jitter. We do want to slow down so server can catch up. This decides how much many % we can slow down compared to server speed. Slowing down 100% is a bit too obvious because it would show a hard stop.")]
        [Range(0, 1)] public float maximumSlowdownRatio = 0.5f; // 50% looks good in the Billiards demo

        [Header("Dampening")]
        [Tooltip("Locally applied force is slowed down a bit compared to the server force, to make catch up more smooth.")]
        [Range(0.05f, 1)] public float localForceDampening = 0.2f; // 50% is too obvious

        [Header("Collision Chaining")]
        [Tooltip("Enable to have actively predicted Rigidbodies activate other Rigidbodies they collide with.")]
        public bool collisionChaining = true;
        [Tooltip("If a player interacts with an object, it can recursively activate other objects it collides with.\nDepth is the chain link from A->B->C->etc., it doesn't mean the amount of A->B, A->C, etc.\nNeeds to be finite to avoid chain activations going on forever like A->B->C->A (easy to notice in the stacked prediction example.")]
        public int maxCollisionChainingDepth = 2; // A->B->C is enough!
        int remainingCollisionChainDepth;

        [Header("Local Delay")]
        [Tooltip("Intentionally add delay before applying forces on client, in order to minimize prediction drift while waiting for server state.\nThe delay is relative to 'RTT'.\nIf we can reduce the prediction time by a hard to notice 50ms worth of physics drift, that's a good thing.")]
        [Range(0,1)] public float localDelayRatio = 0.1f;
        Queue<PendingForce> pendingForces = new Queue<PendingForce>();

        [Header("Debugging")]
        public bool debugColors = false;
        Color originalColor = Color.white;
        public Color predictingColor = Color.green;
        public Color blendingExactColor = Color.yellow;
        public Color blendingGuessColor = Color.red;

        protected override void Awake()
        {
            base.Awake();

            tf = transform;
            rend = GetComponentInChildren<Renderer>();
            predictedRigidbody = GetComponent<Rigidbody>();
            col = GetComponentInChildren<Collider>();
            if (predictedRigidbody == null) throw new InvalidOperationException($"Prediction: {name} is missing a Rigidbody component.");
            predictedRigidbodyTransform = predictedRigidbody.transform;

            initialSyncInterval = syncInterval;

            // make sure predicted physics look smooth
            predictedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            // hard disable only-sync-on-change for now.
            // causes issues where clients objects may be too far off from server pos.
            // and then addforce puts them even further off and they get out of sync forever.
            // because addforce on server thinks the object is elsewhere.
            // TODO instead of only sync on change, we need only sync every 500ms if idle
            onlySyncOnChange = false;

            // save renderer color
            // note if objects share a material, accessing ".material" will
            // instantiate one which can be a massive performance overhead.
            // only use debug colors when debugging!
            if (debugColors) originalColor = rend.material.color;
        }

        public override void OnStartClient()
        {
            // set Rigidbody as kinematic by default on clients.
            // it's only dynamic on the server, and while predicting on clients.
            predictedRigidbody.isKinematic = true;
        }

        void AddPredictedForceInternal(Vector3 force, ForceMode mode)
        {
            // apply a local delay before applying forces.
            // this minimize the prediction+drift workload while waiting for server state.
            double delay = NetworkTime.rtt * localDelayRatio;
            PendingForce pending = new PendingForce
            {
                force = force,
                mode = mode,
                time = NetworkTime.localTime + delay
            };
            pendingForces.Enqueue(pending);
            Debug.Log($"[PREDICTION]: Add Force with {(delay*1000):F0} ms delay");
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

        /*
        void AddPredictedForceChain(Vector3 force, ForceMode mode, int newChainDepth)
        {
            // apply the collision chain depth
            remainingCollisionChainDepth = newChainDepth;

            // add the predicted force
            AddPredictedForceInternal(force, mode);
        }
        */

        public void AddPredictedForceAtPosition(Vector3 force, Vector3 position, ForceMode mode)
        {
            // player interacted with this object explicitly.
            // restart the collision chain at max.
            remainingCollisionChainDepth = maxCollisionChainingDepth;

            // apply a local delay before applying forces.
            // this minimize the prediction+drift workload while waiting for server state.
            double delay = NetworkTime.rtt * localDelayRatio;
            PendingForce pending = new PendingForce
            {
                force = force,
                position = position,
                mode = mode,
                time = NetworkTime.localTime + delay
            };
            pendingForces.Enqueue(pending);
        }

        void ProcessPendingForces()
        {
            while (pendingForces.TryPeek(out PendingForce pending))
            {
                // enough time passed for this force to be applied?
                if (NetworkTime.localTime <= pending.time) return;

                // apply local force dampening.
                // client applies a bit less force than the server, so that
                // catching up to blending state will be easier.
                // for example: dampening = 0.1 means subtract 10% force
                Vector3 force = pending.force * (1 - localForceDampening);

                // explicitly start predicting physics now
                BeginPredicting();

                // apply force, or force at position
                if (pending.position.HasValue)
                {
                    predictedRigidbody.AddForceAtPosition(force, pending.position.Value, pending.mode);
                }
                else
                {
                    predictedRigidbody.AddForce(force, pending.mode);
                }

                // remove it from the queue
                pendingForces.Dequeue();
            }

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
            // Debug.Log($"{name} BEGIN PREDICTING @ {predictionStartTime:F2}");

            // CUSTOM CHANGE: NT is disabled until physics changes to dynamic.
            // by default it gets enabled based on a syncvar.
            // for prediction, we want to enable it immediately when we begin predicting.
            // IMPORTANT: the syncvar also disables it at the right time,
            //            BUT Unity Inspector sometimes doesn't refresh unless clicking A->B->A again. this works fine.
            enabled = true;
            // END CUSTOM CHANGE
        }

        double blendingStartTime;
        double blendingEndTime;
        Vector3 predictionEndPosition = Vector3.zero;
        Quaternion predictionEndRotation = Quaternion.identity;
        protected void BeginBlending()
        {
            // blending interpolates from prediction to expected transform sync.
            // this can be done entirely in memory, without physics.
            predictedRigidbody.isKinematic = true;

            state = ForecastState.BLENDING;
            // if (debugColors) rend.material.color = blendingAheadColor; set in update depending on ahead/behind

            // reset old state
            followingStartPositionEstimate = null;
            followingStartRotationEstimate = null;

            // remember exactly where blending started.
            predictionEndPosition = predictedRigidbody.position;
            predictionEndRotation = predictedRigidbody.rotation;

            // decide exactly when blending starts & ends.
            // we don't want to decide this dynamically base don RTT, which may change while blending.
            // decide it right here, and stick with it.
            // based on NetworkTime.timeline because that's the FOLLOWING timeline.
            blendingStartTime = clientTimeline; // TODO use NetworkTime.time after upgrading NT
            blendingEndTime = blendingStartTime + blendingTime;
            OnBeginBlending();

            // clear any previous snapshots from teleports, old states, while prediction etc.
            // start building a buffer while blending, to later follow while FOLLOWING.
            //clientSnapshots.Clear();
            // Debug.Log($"{name} BEGIN BLENDING");
        }

        protected void BeginFollowing()
        {
            state = ForecastState.FOLLOWING;
            if (debugColors) rend.material.color = originalColor;
            // reset the collision chain depth so it starts at 0 again next time
            remainingCollisionChainDepth = 0;

            // don't disable collisions while following!
            // otherwise player can't interact with it again!
            //  predictedRigidbody.detectCollisions = false;

            OnBeginFollow();
            // Debug.Log($"{name} BEGIN FOLLOW");
        }

        protected override void Update()
        {
            base.Update();
            if (isClientOnly) UpdateClient();
        }

        Vector3 lastSetPosition = Vector3.zero;
        protected override void ApplySnapshot(NTSnapshot interpolated)
        {
            // ignore server snapshots while simulating physics
            if (state == ForecastState.PREDICTING)
            {
                // Rigidboby simulates local physics while predicting.
            }
            // blend between local position and server snapshots
            else if (state == ForecastState.BLENDING)
            {
                // blending is done in UpdateClient()
            }
            // directly apply server snapshots while following
            else if (state == ForecastState.FOLLOWING)
            {
                // BEGIN CUSTOM CHANGE MAGIC: -98% => -1% bots benchmark
                // PERF: only set if changed in order to not trigger physics updates while kinematic(!)
                // EPSILON: simply needs to be small enough so we can't perceive jitter.
                const float epsilon = 0.00001f;
                if (Vector3.Distance(interpolated.position, lastSetPosition) >= epsilon)
                {
                    base.ApplySnapshot(interpolated);
                    // only set if actually changed.
                    // avoids tiny moves accumulating and never being detected as changed move again.
                    lastSetPosition = interpolated.position;
                }
                // END CUSTOM CHANGE
            }
        }

        Vector3? followingStartPositionEstimate;
        Quaternion? followingStartRotationEstimate;

        // Prediction uses a Rigidbody, which needs to be moved in FixedUpdate() even while kinematic.
        double lastReceivedRemoteTime = 0;
        Vector3 lastReceivedRemotePosition = Vector3.zero;
        void UpdateClient()
        {
            // process pending forces
            ProcessPendingForces();

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
                if (lastReceivedRemoteTime > predictionStartTime)
                {
                    // Debug.Log($"{name} END PREDICTING because received state = {lastReceivedState.timestamp:F2} > prediction start = {predictionStartTime:F2}");
                    BeginBlending();
                }
            }
            else if (state == ForecastState.BLENDING)
            {
                // blending finished?
                if (clientTimeline >= blendingEndTime) // TODO use NetworkTime after upgrading NT
                {
                    // Debug.Log($"{name} END BLENDING");
                    BeginFollowing();
                    return;
                }

                // first principles:
                //
                // BLENDING needs to interpolate between PREDICTING & FOLLOWING.
                // the only way to do this without jitter and jumps is by
                // interpolating from PREDICTION.endPosition to FOLLOWING.startPosition.
                // anything else, no matter how smooth, will always cause jumps.
                //
                // PREDICTION.endPosition is easy: just remember before transition.
                //
                // FOLLOWING.startPosition is a bit harder.
                // => we can sample snapshots @ blendingEndTime (if any).
                // => if we haven't received it yet, we need to extrpolate based
                //    on current velocity to guess where we'll be at blendingEndTime.

                // do we have an estimate yet?
                if (!followingStartPositionEstimate.HasValue) return;

                // now we have the exact FOLLOW.startPosition, or a best guess.
                // interpolate from where we started to where we are going.
                // we started at predictionEndPosition @ blendingStartTime.
                // we are going to followStartPosition @ blendingEndTime.
                float totalBlendTime = (float)(blendingEndTime - blendingStartTime);
                float elapsedBlendTime = (float)(blendingEndTime - clientTimeline); // TODO use NetworkTime.time after upgrading NT
                float blendFactor = totalBlendTime > 0 ? Mathf.Clamp01(elapsedBlendTime / totalBlendTime) : 0; // avoids divide by zero

                // interpolate
                Vector3 targetPosition = Vector3.Lerp(followingStartPositionEstimate.Value, predictionEndPosition, blendFactor);
                Quaternion targetRotation = Quaternion.Slerp(followingStartRotationEstimate.Value, predictionEndRotation, blendFactor);

                // set position and rotation
                tf.SetPositionAndRotation(targetPosition, targetRotation);
            }
            // FOLLOWING sets Transform, which happens in Update().
            else if (state == ForecastState.FOLLOWING)
            {
                // NetworkTransform sync happens while FOLLOWING
            }
        }

        // while predicting on client, if we hit another object then we need to
        // start predicting this one too.
        // otherwise the collision response would be delayed until next server
        // state to follow comes in.
        /*
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
        */

        private void OnDrawGizmos()
        {
            Bounds bounds = GetComponent<Collider>().bounds;

            // show the latest received remote position
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(lastReceivedRemotePosition, bounds.size);

            // show the latest blending end position guess
            if (state == ForecastState.BLENDING)
            {
                if (followingStartPositionEstimate.HasValue)
                {
                    Gizmos.color = blendingGuessColor;
                    Gizmos.DrawWireCube(followingStartPositionEstimate.Value, bounds.size);
                }
            }

        }


        // guess FORECASTING.startPosition
        bool GuessForecastingStartPosition(out Vector3 position, out Quaternion rotation)
        {
            // first principles:
            //
            // BLENDING needs to interpolate between PREDICTING & FOLLOWING.
            // the only way to do this without jitter and jumps is by
            // interpolating from PREDICTION.endPosition to FOLLOWING.startPosition.
            // anything else, no matter how smooth, will always cause jumps.
            //
            // PREDICTION.endPosition is easy: just remember before transition.
            //
            // FOLLOWING.startPosition is a bit harder.
            // => we can sample snapshots @ blendingEndTime (if any).
            // => if we haven't received it yet, we need to extrpolate based
            //    on current velocity to guess where we'll be at blendingEndTime.

            position = Vector3.zero;
            rotation = Quaternion.identity;

            // first, see if there's a snapshot at blendingEndTime already.
            // this would be super precise.
            // returns false if there isn't any yet.
            if (SnapshotInterpolation.TrySample(
                clientSnapshots,
                clientTimeline + blendingTime,
                out int from,
                out int to,
                out double t))
            {
                // interpolate between from & to
                NTSnapshot fromSnapshot = clientSnapshots[from];
                NTSnapshot toSnapshot = clientSnapshots[to];
                NTSnapshot interpolated = NTSnapshot.Interpolate(fromSnapshot, toSnapshot, t);
                position = interpolated.position;
                rotation = interpolated.rotation;

                // debug colors
                if (debugColors)
                {
                    rend.material.color = blendingExactColor;
                }

                return true;
            }
            // if not, then we need to guess.
            // calculate the velocity of the latest known state.
            // because that's the most accurate velocity we have.
            // then extrapolate forward to blendingEndTime.
            // TODO UNIT TEST THIS
            else if (clientSnapshots.Count >= 2)
            {
                NTSnapshot latest = clientSnapshots.Values[clientSnapshots.Count - 1];
                NTSnapshot previous = clientSnapshots.Values[clientSnapshots.Count - 2];
                float timeDelta = (float)(latest.remoteTime - previous.remoteTime); // remote time gives us exact remote velocity
                Vector3 positionDelta = latest.position - previous.position;
                Quaternion rotationDelta = (latest.rotation * Quaternion.Inverse(previous.rotation)).normalized; // always need to normalize after mult

                // avoid division by zero
                if (timeDelta > 0)
                {
                    // now we have the remote velocity
                    Vector3 velocity = positionDelta / timeDelta;

                    // extrapolate this from latest time to blendingEndTime
                    // TODO validate rotation formula?
                    float timeToBlendingEnd = (float)(blendingEndTime - clientTimeline);
                    position = latest.position + velocity * timeToBlendingEnd;
                    rotation = latest.rotation * Quaternion.Slerp(Quaternion.identity, rotationDelta, timeToBlendingEnd / timeDelta);

                    // debug colors
                    if (debugColors)
                    {
                        rend.material.color = blendingGuessColor;
                    }

                    return true;
                }
                // this shouldn't really happen. if timedelta is zero: do nothing.
                else
                {
                    return false;
                }
            }
            // if we don't have enough snapshots: do nothing. wait for more.
            else
            {
                return false;
            }
        }

        // optional user callbacks, in case people need to know about events.
        protected virtual void OnBeginPrediction() {}
        protected virtual void OnBeginBlending() {}
        protected virtual void OnBeginFollow() {}

        // process a received server state.
        // compares it against our history and applies corrections if needed.
        protected override void OnServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale)
        {
            base.OnServerToClientSync(position, rotation, scale);

            // store last time
            lastReceivedRemoteTime = NetworkClient.connection.remoteTimeStamp;

            // position might be null if same as last to save bandwidth
            if (position.HasValue) lastReceivedRemotePosition = position.Value;

            // recalculate the FORECASTING.startPosition estimate is only needed
            // whenever we receive a new state. doing it in update() would be wasteful.
            if (state == ForecastState.BLENDING)
            {
                if (GuessForecastingStartPosition(out Vector3 followStartPosition, out Quaternion followStartRotation))
                {
                    followingStartPositionEstimate = followStartPosition;
                    followingStartRotationEstimate = followStartRotation;
                }
                else
                {
                    followingStartPositionEstimate = null;
                    followingStartRotationEstimate = null;
                }
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            // force syncDirection to be ServerToClient
            syncDirection = SyncDirection.ServerToClient;
        }
    }
}
