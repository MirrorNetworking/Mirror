// make sure to use a reasonable sync interval.
// for example, correcting every 100ms seems reasonable.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    struct RigidbodyState : PredictedState
    {
        public double timestamp { get; private set; }

        // we want to store position delta (last + delta = current), and current.
        // this way we can apply deltas on top of corrected positions to get the corrected final position.
        public Vector3    positionDelta; // delta to get from last to this position
        public Vector3    position;

        public Quaternion rotation; // TODO delta rotation?

        public Vector3 velocityDelta; // delta to get from last to this velocity
        public Vector3 velocity;

        public RigidbodyState(
            double timestamp,
            Vector3 positionDelta, Vector3 position,
            Quaternion rotation,
            Vector3 velocityDelta, Vector3 velocity)
        {
            this.timestamp     = timestamp;
            this.positionDelta = positionDelta;
            this.position      = position;
            this.rotation      = rotation;
            this.velocityDelta = velocityDelta;
            this.velocity      = velocity;
        }

        // adjust the deltas after inserting a correction between this one and the previous one.
        public void AdjustDeltas(float multiplier)
        {
            positionDelta = Vector3.Lerp(Vector3.zero, positionDelta, multiplier);
            // TODO if we have have a rotation delta, then scale it here too
            velocityDelta = Vector3.Lerp(Vector3.zero, velocityDelta, multiplier);
        }

        public static RigidbodyState Interpolate(RigidbodyState a, RigidbodyState b, float t)
        {
            return new RigidbodyState
            {
                position = Vector3.Lerp(a.position, b.position, t),
                rotation = Quaternion.Slerp(a.rotation, b.rotation, t),
                velocity = Vector3.Lerp(a.velocity, b.velocity, t)
            };
        }
    }

    public enum CorrectionMode
    {
        Set,               // rigidbody.position/rotation = ...
        Move,              // rigidbody.MovePosition/Rotation
    }

    [RequireComponent(typeof(Rigidbody))]
    public class PredictedRigidbody : NetworkBehaviour
    {
        Rigidbody rb;
        Vector3 lastPosition;

        // [Tooltip("Broadcast changes if position changed by more than ... meters.")]
        // public float positionSensitivity = 0.01f;

        // client keeps state history for correction & reconciliation
        [Header("State History")]
        public int stateHistoryLimit = 32; // 32 x 50 ms = 1.6 seconds is definitely enough
        readonly SortedList<double, RigidbodyState> stateHistory = new SortedList<double, RigidbodyState>();

        [Header("Reconciliation")]
        [Tooltip("Correction threshold in meters. For example, 0.1 means that if the client is off by more than 10cm, it gets corrected.")]
        public double correctionThreshold = 0.10;

        [Tooltip("Applying server corrections one frame ahead gives much better results. We don't know why yet, so this is an option for now.")]
        public bool oneFrameAhead = true;

        [Header("Smoothing")]
        [Tooltip("Configure how to apply the corrected state.")]
        public CorrectionMode correctionMode = CorrectionMode.Move;

        [Header("Debugging")]
        public float lineTime = 10;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

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

        void Update()
        {
            if (isServer) UpdateServer();
        }

        void FixedUpdate()
        {
            // record client state every FixedUpdate
            if (isClient) RecordState();
        }

        void ApplyState(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
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

            rb.velocity = velocity;
        }

        // record state at NetworkTime.time on client
        void RecordState()
        {
            // NetworkTime.time is always behind by bufferTime.
            // prediction aims to be on the exact same server time (immediately).
            // use predictedTime to record state, otherwise we would record in the past.
            double predictedTime = NetworkTime.predictedTime;

            // TODO FixedUpdate may run twice in the same frame / NetworkTime.time.
            // for now, simply don't record if already recorded there.
            if (stateHistory.ContainsKey(predictedTime))
                return;

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

            // add state to history
            stateHistory.Add(
                predictedTime,
                new RigidbodyState(
                    predictedTime,
                    positionDelta, rb.position,
                    rb.rotation,
                    velocityDelta, rb.velocity)
            );
        }

        void ApplyCorrection(RigidbodyState corrected, RigidbodyState before, RigidbodyState after)
        {
            // TODO merge this with CompareState iteration!

            // first, remember the delta between last recorded state and current live state.
            // before we potentially correct 'last' in history.
            // TODO we always record the current state in CompareState now.
            //      applying live delta may not be necessary anymore.
            //      this should always be '0' now.
            // RigidbodyState newest = stateHistory.Values[stateHistory.Count - 1];
            // Vector3 livePositionDelta = rb.position - newest.position;
            // Vector3 liveVelocityDelta = rb.velocity - newest.velocity;
            // TODO rotation delta?

            // insert the corrected state and adjust 'after.delta' to the inserted.
            Prediction.InsertCorrection(stateHistory, stateHistoryLimit, corrected, before, after);

            // show the received correction position + velocity for debugging.
            // helps to compare with the interpolated/applied correction locally.
            // TODO don't hardcode length?
            Debug.DrawLine(corrected.position, corrected.position + corrected.velocity * 0.1f, Color.white, lineTime);

            // now go through the history:
            // 1. skip all states before the inserted / corrected entry
            // 3. apply all deltas after timestamp
            // 4. recalculate corrected position based on inserted + sum(deltas)
            // 5. apply rigidbody correction
            RigidbodyState last = corrected;
            int correctedCount = 0; // for debugging
            for (int i = 0; i < stateHistory.Count; ++i)
            {
                double key = stateHistory.Keys[i];
                RigidbodyState entry = stateHistory.Values[i];

                // skip all states before (and including) the corrected entry
                // TODO InsertCorrection() above should return the inserted index to skip faster.
                if (key <= corrected.timestamp)
                    continue;

                // this state is after the inserted state.
                // correct it's absolute position based on last + delta.
                entry.position = last.position + entry.positionDelta;
                // TODO rotation
                entry.velocity = last.velocity + entry.velocityDelta;

                // save the corrected entry into history.
                // if we don't, then corrections for [i+1] would compare the
                // uncorrected state and attempt to correct again, resulting in
                // noticeable jitter and displacements.
                //
                // not saving it would also result in objects flying towards
                // infinity when using sendInterval = 0.
                stateHistory[entry.timestamp] = entry;

                // debug draw the corrected state
                // Debug.DrawLine(last.position, entry.position, Color.cyan, lineTime);

                // save last
                last = entry;
                correctedCount += 1;
            }

            // log, draw & apply the final position.
            // always do this here, not when iterating above, in case we aren't iterating.
            // for example, on same machine with near zero latency.
            Debug.Log($"Correcting {name}: {correctedCount} / {stateHistory.Count} states to final position from: {rb.position} to: {last.position}");
            Debug.DrawLine(rb.position, last.position, Color.green, lineTime);
            ApplyState(last.position, last.rotation, last.velocity);
        }

        // compare client state with server state at timestamp.
        // apply correction if necessary.
        void CompareState(double timestamp, RigidbodyState state)
        {
            // we only capture state every 'interval' milliseconds.
            // so the newest entry in 'history' may be up to 'interval' behind 'now'.
            // if there's no latency, we may receive a server state for 'now'.
            // sampling would fail, if we haven't recorded anything in a while.
            // to solve this, always record the current state when receiving a server state.
            RecordState();

            // find the two closest client states between timestamp
            if (!Prediction.Sample(stateHistory, timestamp, out RigidbodyState before, out RigidbodyState after, out double t))
            {
                // if we failed to sample, that could indicate a problem.
                // first, if the client didn't record 'limit' entries yet, then
                // let it keep recording. it'll be fine.
                if (stateHistory.Count < stateHistoryLimit) return;

                // if we are already at the recording limit and still can't
                // sample, then that's a problem.
                // there are two cases to consider.
                RigidbodyState oldest = stateHistory.Values[0];
                RigidbodyState newest = stateHistory.Values[stateHistory.Count - 1];

                // is the state older than the oldest state in history?
                // this can happen if the client gets so far behind the server
                // that it doesn't have a recored history to sample from.
                // in that case, we should hard correct the client.
                // otherwise it could be out of sync as long as it's too far behind.
                if (state.timestamp < oldest.timestamp)
                {
                    Debug.LogWarning($"Hard correcting client because the client is too far behind the server. History of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This would cause the client to be out of sync as long as it's behind.");
                    ApplyCorrection(state, state, state);
                }
                // is it newer than the newest state in history?
                // this can happen if client's predictedTime predicts too far ahead of the server.
                // in that case, log a warning for now but still apply the correction.
                // otherwise it could be out of sync as long as it's too far ahead.
                //
                // for example, when running prediction on the same machine with near zero latency.
                // when applying corrections here, this looks just fine on the local machine.
                else if (newest.timestamp < state.timestamp)
                {
                    // the correction is for a state in the future.
                    // we clamp it to 'now'.
                    // but only correct if off by threshold.
                    // TODO maybe we should interpolate this back to 'now'?
                    if (Vector3.Distance(state.position, rb.position) >= correctionThreshold)
                    {
                        double ahead = state.timestamp - newest.timestamp;
                        Debug.Log($"Hard correction because the client is ahead of the server by {(ahead*1000):F1}ms. History of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This can happen when latency is near zero, and is fine unless it shows jitter.");
                        ApplyCorrection(state, state, state);
                    }
                }
                // otherwise something went very wrong. sampling should've worked.
                // hard correct to recover the error.
                else
                {
                    // TODO
                    Debug.LogError($"Failed to sample history of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This should never happen because the timestamp is within history.");
                    ApplyCorrection(state, state, state);
                }

                // either way, nothing more to do here
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
                ApplyCorrection(state, before, after);
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

            // compare state without deltas
            CompareState(timestamp, new RigidbodyState(timestamp, Vector3.zero, position, rotation, Vector3.zero, velocity));
        }
    }
}
