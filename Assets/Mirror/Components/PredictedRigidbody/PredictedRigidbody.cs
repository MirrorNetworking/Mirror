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
using UnityEngine;

namespace Mirror
{
    public enum CorrectionMode
    {
        Set,               // rigidbody.position/rotation = ...
        Move,              // rigidbody.MovePosition/Rotation
    }

    [Obsolete("Prediction is under development, do not use this yet.")]
    [RequireComponent(typeof(Rigidbody))]
    public class PredictedRigidbody : NetworkBehaviour
    {
        Rigidbody rb;
        Vector3 lastPosition;

        // [Tooltip("Broadcast changes if position changed by more than ... meters.")]
        // public float positionSensitivity = 0.01f;

        // client keeps state history for correction & reconciliation.
        // this needs to be a SortedList because we need to be able to insert inbetween.
        // RingBuffer would be faster iteration, but can't do insertions.
        [Header("State History")]
        public int stateHistoryLimit = 32; // 32 x 50 ms = 1.6 seconds is definitely enough
        readonly SortedList<double, RigidbodyState> stateHistory = new SortedList<double, RigidbodyState>();

        [Tooltip("(Optional) performance optimization where received state is compared to the LAST recorded state first, before sampling the whole history.\n\nThis can save significant traversal overhead for idle objects with a tiny chance of missing corrections for objects which revisisted the same position in the recent history twice.")]
        public bool compareLastFirst = true;

        [Header("Reconciliation")]
        [Tooltip("Correction threshold in meters. For example, 0.1 means that if the client is off by more than 10cm, it gets corrected.")]
        public double correctionThreshold = 0.10;

        [Tooltip("Applying server corrections one frame ahead gives much better results. We don't know why yet, so this is an option for now.")]
        public bool oneFrameAhead = true;

        [Header("Smoothing")]
        [Tooltip("Configure how to apply the corrected state.")]
        public CorrectionMode correctionMode = CorrectionMode.Move;

        [Tooltip("Server & Client would sometimes fight over the final position at rest. Instead, hard snap into black below a certain velocity threshold.")]
        public float snapThreshold = 0.5f; // adjust with log messages ('snap'). '2' works, but '0.5' is fine too.

        [Header("Visual Interpolation")]
        [Tooltip("After creating the visual interpolation object, keep showing the original Rigidbody with a ghost (transparent) material for debugging.")]
        public bool showGhost = true;
        public float ghostDistanceThreshold = 0.1f;
        public float ghostEnabledCheckInterval = 0.2f;
        double lastGhostEnabledCheckTime = 0;

        [Tooltip("After creating the visual interpolation object, replace this object's renderer materials with the ghost (ideally transparent) material.")]
        public Material ghostMaterial;

        [Tooltip("How fast to interpolate to the target position, relative to how far we are away from it.\nHigher value will be more jitter but sharper moves, lower value will be less jitter but a little too smooth / rounded moves.")]
        public float positionInterpolationSpeed = 15; // 10 is a little too low for billiards at least
        public float rotationInterpolationSpeed = 10;

        [Tooltip("Teleport if we are further than 'multiplier x collider size' behind.")]
        public float teleportDistanceMultiplier = 10;

        [Header("Debugging")]
        public float lineTime = 10;

        // visually interpolated GameObject copy for smoothing
        protected GameObject visualCopy;
        protected MeshRenderer[] originalRenderers;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        // instantiate a visually-only copy of the gameobject to apply smoothing.
        // on clients, where players are watching.
        // create & destroy methods are virtual so games with a different
        // rendering setup / hierarchy can inject their own copying code here.
        protected virtual void CreateVisualCopy()
        {
            // create an empty GameObject with the same name + _Visual
            visualCopy = new GameObject($"{name}_Visual");
            visualCopy.transform.position = transform.position;
            visualCopy.transform.rotation = transform.rotation;
            visualCopy.transform.localScale = transform.localScale;

            // add the PredictedRigidbodyVisual component
            PredictedRigidbodyVisual visualRigidbody = visualCopy.AddComponent<PredictedRigidbodyVisual>();
            visualRigidbody.target = this;
            visualRigidbody.positionInterpolationSpeed = positionInterpolationSpeed;
            visualRigidbody.rotationInterpolationSpeed = rotationInterpolationSpeed;
            visualRigidbody.teleportDistanceMultiplier = teleportDistanceMultiplier;

            // copy the rendering components
            if (TryGetComponent(out MeshRenderer originalMeshRenderer))
            {
                MeshFilter meshFilter = visualCopy.AddComponent<MeshFilter>();
                meshFilter.mesh = GetComponent<MeshFilter>().mesh;

                MeshRenderer meshRenderer = visualCopy.AddComponent<MeshRenderer>();
                meshRenderer.material = originalMeshRenderer.material;

                // renderers often have multiple materials. copy all.
                if (originalMeshRenderer.materials != null)
                {
                    Material[] materials = new Material[originalMeshRenderer.materials.Length];
                    for (int i = 0; i < materials.Length; ++i)
                    {
                        materials[i] = originalMeshRenderer.materials[i];
                    }
                    meshRenderer.materials = materials; // need to reassign to see it in effect
                }
            }
            // if we didn't find a renderer, show a warning
            else Debug.LogWarning($"PredictedRigidbody: {name} found no renderer to copy onto the visual object. If you are using a custom setup, please overwrite PredictedRigidbody.CreateVisualCopy().");

            // find renderers in children.
            // this will be used a lot later, so only find them once when
            // creating the visual copy here.
            originalRenderers = GetComponentsInChildren<MeshRenderer>();

            // replace this renderer's materials with the ghost (if enabled)
            foreach (MeshRenderer rend in originalRenderers)
            {
                if (showGhost)
                {
                    // renderers often have multiple materials. replace all.
                    if (rend.materials != null)
                    {
                        Material[] materials = rend.materials;
                        for (int i = 0; i < materials.Length; ++i)
                        {
                            materials[i] = ghostMaterial;
                        }
                        rend.materials = materials; // need to reassign to see it in effect
                    }
                }
                else
                {
                    rend.enabled = false;
                }
            }
        }

        protected virtual void DestroyVisualCopy()
        {
            if (visualCopy != null) Destroy(visualCopy);
        }

        protected virtual void UpdateVisualCopy()
        {
            // only if visual copy was already created
            if (visualCopy == null || originalRenderers == null) return;

            // enough to run this in a certain interval.
            // doing this every update would be overkill.
            // this is only for debug purposes anyway.
            if (NetworkTime.localTime < lastGhostEnabledCheckTime + ghostEnabledCheckInterval) return;
            lastGhostEnabledCheckTime = NetworkTime.localTime;

            // only show ghost while interpolating towards the object.
            // if we are 'inside' the object then don't show ghost.
            // otherwise it just looks like z-fighting the whole time.
            // => iterated the renderers we found when creating the visual copy.
            //    we don't want to GetComponentsInChildren every time here!
            bool insideTarget = Vector3.Distance(transform.position, visualCopy.transform.position) <= ghostDistanceThreshold;
            foreach (MeshRenderer rend in originalRenderers)
                rend.enabled = !insideTarget;
        }

        // creater visual copy only on clients, where players are watching.
        public override void OnStartClient() => CreateVisualCopy();

        // destroy visual copy only in OnStopClient().
        // OnDestroy() wouldn't be called for scene objects that are only disabled instead of destroyed.
        public override void OnStopClient() => DestroyVisualCopy();

        void UpdateServer()
        {
            // to save bandwidth, we only serialize when position changed
            // if (Vector3.Distance(transform.position, lastPosition) >= positionSensitivity)
            // {
            //     lastPosition = transform.position;
            //     SetDirty();
            // }

            // always set dirty to always serialize.
            // fixes issues where an object was idle and stopped serializing on server,
            // even though it was still moving on client.
            // hence getting totally out of sync.
            SetDirty();
        }

        void UpdateClient()
        {
            UpdateVisualCopy();
        }

        void Update()
        {
            if (isServer) UpdateServer();
            if (isClient) UpdateClient();
        }

        void FixedUpdate()
        {
            // on clients we record the current state every FixedUpdate.
            // this is cheap, and allows us to keep a dense history.
            if (isClient) RecordState();
        }

        // manually store last recorded so we can easily check against this
        // without traversing the SortedList.
        RigidbodyState lastRecorded;
        void RecordState()
        {
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

            // calculate delta to previous state (if any)
            Vector3 positionDelta = Vector3.zero;
            Vector3 velocityDelta = Vector3.zero;
            if (stateHistory.Count > 0)
            {
                RigidbodyState last = stateHistory.Values[stateHistory.Count - 1];
                positionDelta = rb.position - last.position;
                velocityDelta = rb.velocity - last.velocity;

                // debug draw the recorded state
                Debug.DrawLine(last.position, rb.position, Color.red, lineTime);
            }

            // create state to insert
            RigidbodyState state = new RigidbodyState(
                predictedTime,
                positionDelta, rb.position,
                rb.rotation,
                velocityDelta, rb.velocity
            );

            // add state to history
            stateHistory.Add(predictedTime, state);

            // manually remember last inserted state for faster .Last comparisons
            lastRecorded = state;
        }

        void ApplyState(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            // fix rigidbodies seemingly dancing in place instead of coming to rest.
            // hard snap to the position below a threshold velocity.
            // this is fine because the visual object still smoothly interpolates to it.
            if (rb.velocity.magnitude <= snapThreshold)
            {
                Debug.Log($"Prediction: snapped {name} into place because velocity {rb.velocity.magnitude:F3} <= {snapThreshold:F3}");
                stateHistory.Clear();
                rb.position = position;
                rb.rotation = rotation;
                rb.velocity = Vector3.zero;
                return;
            }

            // Rigidbody .position teleports, while .MovePosition interpolates
            // TODO is this a good idea? what about next capture while it's interpolating?
            if (correctionMode == CorrectionMode.Move)
            {
                rb.MovePosition(position);
                rb.MoveRotation(rotation);
            }
            else if (correctionMode == CorrectionMode.Set)
            {
                rb.position = position;
                rb.rotation = rotation;
            }

            // there's only one way to set velocity
            rb.velocity = velocity;
        }

        // process a received server state.
        // compares it against our history and applies corrections if needed.
        void OnReceivedState(double timestamp, RigidbodyState state)
        {
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
            if (compareLastFirst && Vector3.Distance(state.position, rb.position) < correctionThreshold)
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
                Debug.LogWarning($"Hard correcting client because the client is too far behind the server. History of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This would cause the client to be out of sync as long as it's behind.");
                ApplyState(state.position, state.rotation, state.velocity);
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
                if (Vector3.Distance(state.position, rb.position) >= correctionThreshold)
                {
                    double ahead = state.timestamp - newest.timestamp;
                    Debug.Log($"Hard correction because the client is ahead of the server by {(ahead*1000):F1}ms. History of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This can happen when latency is near zero, and is fine unless it shows jitter.");
                    ApplyState(state.position, state.rotation, state.velocity);
                }
                return;
            }

            // find the two closest client states between timestamp
            if (!Prediction.Sample(stateHistory, timestamp, out RigidbodyState before, out RigidbodyState after, out int afterIndex, out double t))
            {
                // something went very wrong. sampling should've worked.
                // hard correct to recover the error.
                Debug.LogError($"Failed to sample history of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This should never happen because the timestamp is within history.");
                ApplyState(state.position, state.rotation, state.velocity);
                return;
            }

            // interpolate between them to get the best approximation
            RigidbodyState interpolated = RigidbodyState.Interpolate(before, after, (float)t);

            // calculate the difference between where we were and where we should be
            // TODO only position for now. consider rotation etc. too later
            float difference = Vector3.Distance(state.position, interpolated.position);
            // Debug.Log($"Sampled history of size={stateHistory.Count} @ {timestamp:F3}: client={interpolated.position} server={state.position} difference={difference:F3} / {correctionThreshold:F3}");

            // too far off? then correct it
            if (difference >= correctionThreshold)
            {
                // Debug.Log($"CORRECTION NEEDED FOR {name} @ {timestamp:F3}: client={interpolated.position} server={state.position} difference={difference:F3}");

                // show the received correction position + velocity for debugging.
                // helps to compare with the interpolated/applied correction locally.
                Debug.DrawLine(state.position, state.position + state.velocity * 0.1f, Color.white, lineTime);

                // insert the correction and correct the history on top of it.
                // returns the final recomputed state after rewinding.
                RigidbodyState recomputed = Prediction.CorrectHistory(stateHistory, stateHistoryLimit, state, before, after, afterIndex);

                // log, draw & apply the final position.
                // always do this here, not when iterating above, in case we aren't iterating.
                // for example, on same machine with near zero latency.
                // int correctedAmount = stateHistory.Count - afterIndex;
                // Debug.Log($"Correcting {name}: {correctedAmount} / {stateHistory.Count} states to final position from: {rb.position} to: {last.position}");
                Debug.DrawLine(rb.position, recomputed.position, Color.green, lineTime);
                ApplyState(recomputed.position, recomputed.rotation, recomputed.velocity);
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
            writer.WriteFloat(Time.deltaTime);
            writer.WriteVector3(rb.position);
            writer.WriteQuaternion(rb.rotation);
            writer.WriteVector3(rb.velocity);
        }

        // read the server's state, compare with client state & correct if necessary.
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // deserialize data
            // we want to know the time on the server when this was sent, which is remoteTimestamp.
            double timestamp = NetworkClient.connection.remoteTimeStamp;

            // server send state at the end of the frame.
            // parse and apply the server's delta time to our timestamp.
            // otherwise we see noticeable resets that seem off by one frame.
            double serverDeltaTime = reader.ReadFloat();
            timestamp += serverDeltaTime;

            // however, adding yet one more frame delay gives much(!) better results.
            // we don't know why yet, so keep this as an option for now.
            // possibly because client captures at the beginning of the frame,
            // with physics happening at the end of the frame?
            if (oneFrameAhead) timestamp += serverDeltaTime;

            // parse state
            Vector3 position    = reader.ReadVector3();
            Quaternion rotation = reader.ReadQuaternion();
            Vector3 velocity    = reader.ReadVector3();

            // process received state
            OnReceivedState(timestamp, new RigidbodyState(timestamp, Vector3.zero, position, rotation, Vector3.zero, velocity));
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            // force syncDirection to be ServerToClient
            syncDirection = SyncDirection.ServerToClient;
        }
    }
}
