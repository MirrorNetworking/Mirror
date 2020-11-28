// vis2k:
// base class for NetworkTransform and NetworkTransformChild.
// New method is simple and stupid. No more 1500 lines of code.
//
// Server sends current data.
// Client saves it and interpolates last and latest data points.
//   Update handles transform movement / rotation
//   FixedUpdate handles rigidbody movement / rotation
//
// Notes:
// * Built-in Teleport detection in case of lags / teleport / obstacles
// * Quaternion > EulerAngles because gimbal lock and Quaternion.Slerp
// * Syncs XYZ. Works 3D and 2D. Saving 4 bytes isn't worth 1000 lines of code.
// * Initial delay might happen if server sends packet immediately after moving
//   just 1cm, hence we move 1cm and then wait 100ms for next packet
// * Only way for smooth movement is to use a fixed movement speed during
//   interpolation. interpolation over time is never that good.
//
using System;
using UnityEngine;

namespace Mirror.Experimental
{
    public abstract class NetworkTransformBase : NetworkBehaviour
    {
        // target transform to sync. can be on a child.
        protected abstract Transform targetTransform { get; }

        [Header("Authority")]

        [Tooltip("Set to true if moves come from owner client, set to false if moves always come from server")]
        [SyncVar]
        public bool clientAuthority;

        [Tooltip("Set to true if updates from server should be ignored by owner")]
        [SyncVar]
        public bool excludeOwnerUpdate = true;

        [Header("Synchronization")]

        [Tooltip("Set to true if position should be synchronized")]
        [SyncVar]
        public bool syncPosition = true;

        [Tooltip("Set to true if rotation should be synchronized")]
        [SyncVar]
        public bool syncRotation = true;

        [Tooltip("Set to true if scale should be synchronized")]
        [SyncVar]
        public bool syncScale = true;

        [Header("Interpolation")]

        [Tooltip("Set to true if position should be interpolated")]
        [SyncVar]
        public bool interpolatePosition = true;

        [Tooltip("Set to true if rotation should be interpolated")]
        [SyncVar]
        public bool interpolateRotation = true;

        [Tooltip("Set to true if scale should be interpolated")]
        [SyncVar]
        public bool interpolateScale = true;

        // Sensitivity is added for VR where human players tend to have micro movements so this can quiet down
        // the network traffic.  Additionally, rigidbody drift should send less traffic, e.g very slow sliding / rolling.
        [Header("Sensitivity")]

        [Tooltip("Changes to the transform must exceed these values to be transmitted on the network.")]
        [SyncVar]
        public float localPositionSensitivity = .01f;

        [Tooltip("If rotation exceeds this angle, it will be transmitted on the network")]
        [SyncVar]
        public float localRotationSensitivity = .01f;

        [Tooltip("Changes to the transform must exceed these values to be transmitted on the network.")]
        [SyncVar]
        public float localScaleSensitivity = .01f;

        [Header("Diagnostics")]

        // server
        public Vector3 lastPosition;
        public Quaternion lastRotation;
        public Vector3 lastScale;

        // client
        // use local position/rotation for VR support
        [Serializable]
        public struct DataPoint
        {
            public float timeStamp;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
            public float movementSpeed;

            public bool isValid => timeStamp != 0;
        }

        // Is this a client with authority over this transform?
        // This component could be on the player object or any object that has been assigned authority to this client.
        bool IsOwnerWithClientAuthority => hasAuthority && clientAuthority;

        // interpolation start and goal
        public DataPoint start = new DataPoint();
        public DataPoint goal = new DataPoint();

        // We need to store this locally on the server so clients can't request Authority when ever they like
        bool clientAuthorityBeforeTeleport;

        void FixedUpdate()
        {
            // if server then always sync to others.
            // let the clients know that this has moved
            if (isServer && HasEitherMovedRotatedScaled())
            {
                ServerUpdate();
            }

            if (isClient)
            {
                // send to server if we have local authority (and aren't the server)
                // -> only if connectionToServer has been initialized yet too
                if (IsOwnerWithClientAuthority)
                {
                    ClientAuthorityUpdate();
                }
                else if (goal.isValid)
                {
                    ClientRemoteUpdate();
                }
            }
        }

        void ServerUpdate()
        {
            RpcMove(targetTransform.localPosition, Compression.CompressQuaternion(targetTransform.localRotation), targetTransform.localScale);
        }

        void ClientAuthorityUpdate()
        {
            if (!isServer && HasEitherMovedRotatedScaled())
            {
                // serialize
                // local position/rotation for VR support
                // send to server
                CmdClientToServerSync(targetTransform.localPosition, Compression.CompressQuaternion(targetTransform.localRotation), targetTransform.localScale);
            }
        }

        void ClientRemoteUpdate()
        {
            // teleport or interpolate
            if (NeedsTeleport())
            {
                // local position/rotation for VR support
                ApplyPositionRotationScale(goal.localPosition, goal.localRotation, goal.localScale);

                // reset data points so we don't keep interpolating
                start = new DataPoint();
                goal = new DataPoint();
            }
            else
            {
                // local position/rotation for VR support
                ApplyPositionRotationScale(InterpolatePosition(start, goal, targetTransform.localPosition),
                                           InterpolateRotation(start, goal, targetTransform.localRotation),
                                           InterpolateScale(start, goal, targetTransform.localScale));
            }
        }

        // moved or rotated or scaled since last time we checked it?
        bool HasEitherMovedRotatedScaled()
        {
            // Save last for next frame to compare only if change was detected, otherwise
            // slow moving objects might never sync because of C#'s float comparison tolerance.
            // See also: https://github.com/vis2k/Mirror/pull/428)
            bool changed = HasMoved || HasRotated || HasScaled;
            if (changed)
            {
                // local position/rotation for VR support
                if (syncPosition) lastPosition = targetTransform.localPosition;
                if (syncRotation) lastRotation = targetTransform.localRotation;
                if (syncScale) lastScale = targetTransform.localScale;
            }
            return changed;
        }

        // local position/rotation for VR support
        // SqrMagnitude is faster than Distance per Unity docs
        // https://docs.unity3d.com/ScriptReference/Vector3-sqrMagnitude.html

        bool HasMoved => syncPosition && Vector3.SqrMagnitude(lastPosition - targetTransform.localPosition) > localPositionSensitivity * localPositionSensitivity;
        bool HasRotated => syncRotation && Quaternion.Angle(lastRotation, targetTransform.localRotation) > localRotationSensitivity;
        bool HasScaled => syncScale && Vector3.SqrMagnitude(lastScale - targetTransform.localScale) > localScaleSensitivity * localScaleSensitivity;

        // teleport / lag / stuck detection
        // - checking distance is not enough since there could be just a tiny fence between us and the goal
        // - checking time always works, this way we just teleport if we still didn't reach the goal after too much time has elapsed
        bool NeedsTeleport()
        {
            // calculate time between the two data points
            float startTime = start.isValid ? start.timeStamp : Time.time - Time.fixedDeltaTime;
            float goalTime = goal.isValid ? goal.timeStamp : Time.time;
            float difference = goalTime - startTime;
            float timeSinceGoalReceived = Time.time - goalTime;
            return timeSinceGoalReceived > difference * 5;
        }

        // local authority client sends sync message to server for broadcasting
        [Command(channel = Channels.DefaultUnreliable)]
        void CmdClientToServerSync(Vector3 position, uint packedRotation, Vector3 scale)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
                return;

            // deserialize payload
            SetGoal(position, Compression.DecompressQuaternion(packedRotation), scale);

            // server-only mode does no interpolation to save computations, but let's set the position directly
            if (isServer && !isClient)
                ApplyPositionRotationScale(goal.localPosition, goal.localRotation, goal.localScale);

            RpcMove(position, packedRotation, scale);
        }

        [ClientRpc(channel = Channels.DefaultUnreliable)]
        void RpcMove(Vector3 position, uint packedRotation, Vector3 scale)
        {
            if (hasAuthority && excludeOwnerUpdate) return;

            if (!isServer)
                SetGoal(position, Compression.DecompressQuaternion(packedRotation), scale);
        }

        // serialization is needed by OnSerialize and by manual sending from authority
        void SetGoal(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // put it into a data point immediately
            DataPoint temp = new DataPoint
            {
                // deserialize position
                localPosition = position,
                localRotation = rotation,
                localScale = scale,
                timeStamp = Time.time
            };

            // movement speed: based on how far it moved since last time has to be calculated before 'start' is overwritten
            temp.movementSpeed = EstimateMovementSpeed(goal, temp, targetTransform, Time.fixedDeltaTime);

            // reassign start wisely
            // first ever data point? then make something up for previous one so that we can start interpolation without waiting for next.
            if (start.timeStamp == 0)
            {
                start = new DataPoint
                {
                    timeStamp = Time.time - Time.fixedDeltaTime,
                    // local position/rotation for VR support
                    localPosition = targetTransform.localPosition,
                    localRotation = targetTransform.localRotation,
                    localScale = targetTransform.localScale,
                    movementSpeed = temp.movementSpeed
                };
            }
            // second or nth data point? then update previous
            // but: we start at where ever we are right now, so that it's perfectly smooth and we don't jump anywhere
            //
            //    example if we are at 'x':
            //
            //        A--x->B
            //
            //    and then receive a new point C:
            //
            //        A--x--B
            //              |
            //              |
            //              C
            //
            //    then we don't want to just jump to B and start interpolation:
            //
            //              x
            //              |
            //              |
            //              C
            //
            //    we stay at 'x' and interpolate from there to C:
            //
            //           x..B
            //            \ .
            //             \.
            //              C
            //
            else
            {
                float oldDistance = Vector3.Distance(start.localPosition, goal.localPosition);
                float newDistance = Vector3.Distance(goal.localPosition, temp.localPosition);

                start = goal;

                // local position/rotation for VR support
                // teleport / lag / obstacle detection: only continue at current position if we aren't too far away
                // XC  < AB + BC (see comments above)
                if (Vector3.Distance(targetTransform.localPosition, start.localPosition) < oldDistance + newDistance)
                {
                    start.localPosition = targetTransform.localPosition;
                    start.localRotation = targetTransform.localRotation;
                    start.localScale = targetTransform.localScale;
                }
            }

            // set new destination in any case. new data is best data.
            goal = temp;
        }

        // try to estimate movement speed for a data point based on how far it moved since the previous one
        // - if this is the first time ever then we use our best guess:
        //     - delta based on transform.localPosition
        //     - elapsed based on send interval hoping that it roughly matches
        static float EstimateMovementSpeed(DataPoint from, DataPoint to, Transform transform, float sendInterval)
        {
            Vector3 delta = to.localPosition - (from.localPosition != transform.localPosition ? from.localPosition : transform.localPosition);
            float elapsed = from.isValid ? to.timeStamp - from.timeStamp : sendInterval;

            // avoid NaN
            return elapsed > 0 ? delta.magnitude / elapsed : 0;
        }

        // set position carefully depending on the target component
        void ApplyPositionRotationScale(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // local position/rotation for VR support
            if (syncPosition) targetTransform.localPosition = position;
            if (syncRotation) targetTransform.localRotation = rotation;
            if (syncScale) targetTransform.localScale = scale;
        }

        // where are we in the timeline between start and goal? [0,1]
        Vector3 InterpolatePosition(DataPoint start, DataPoint goal, Vector3 currentPosition)
        {
            if (!interpolatePosition)
                return currentPosition;

            if (start.movementSpeed != 0)
            {
                // Option 1: simply interpolate based on time, but stutter will happen, it's not that smooth.
                // This is especially noticeable if the camera automatically follows the player
                // -         Tell SonarCloud this isn't really commented code but actual comments and to stfu about it
                // -         float t = CurrentInterpolationFactor();
                // -         return Vector3.Lerp(start.position, goal.position, t);

                // Option 2: always += speed
                // speed is 0 if we just started after idle, so always use max for best results
                float speed = Mathf.Max(start.movementSpeed, goal.movementSpeed);
                return Vector3.MoveTowards(currentPosition, goal.localPosition, speed * Time.deltaTime);
            }

            return currentPosition;
        }

        Quaternion InterpolateRotation(DataPoint start, DataPoint goal, Quaternion defaultRotation)
        {
            if (!interpolateRotation)
                return defaultRotation;

            if (start.localRotation != goal.localRotation)
            {
                float t = CurrentInterpolationFactor(start, goal);
                return Quaternion.Slerp(start.localRotation, goal.localRotation, t);
            }

            return defaultRotation;
        }

        Vector3 InterpolateScale(DataPoint start, DataPoint goal, Vector3 currentScale)
        {
            if (!interpolateScale)
                return currentScale;

            if (start.localScale != goal.localScale)
            {
                float t = CurrentInterpolationFactor(start, goal);
                return Vector3.Lerp(start.localScale, goal.localScale, t);
            }

            return currentScale;
        }

        static float CurrentInterpolationFactor(DataPoint start, DataPoint goal)
        {
            if (start.isValid)
            {
                float difference = goal.timeStamp - start.timeStamp;

                // the moment we get 'goal', 'start' is supposed to start, so elapsed time is based on:
                float elapsed = Time.time - goal.timeStamp;

                // avoid NaN
                return difference > 0 ? elapsed / difference : 1;
            }
            return 1;
        }

        #region Server Teleport (force move player)

        /// <summary>
        /// This method will override this GameObject's current Transform.localPosition to the specified Vector3  and update all clients.
        /// <para>NOTE: position must be in LOCAL space if the transform has a parent</para>
        /// </summary>
        /// <param name="localPosition">Where to teleport this GameObject</param>
        [Server]
        public void ServerTeleport(Vector3 localPosition)
        {
            Quaternion localRotation = targetTransform.localRotation;
            ServerTeleport(localPosition, localRotation);
        }

        /// <summary>
        /// This method will override this GameObject's current Transform.localPosition and Transform.localRotation
        /// to the specified Vector3 and Quaternion and update all clients.
        /// <para>NOTE: localPosition must be in LOCAL space if the transform has a parent</para>
        /// <para>NOTE: localRotation must be in LOCAL space if the transform has a parent</para>
        /// </summary>
        /// <param name="localPosition">Where to teleport this GameObject</param>
        /// <param name="localRotation">Which rotation to set this GameObject</param>
        [Server]
        public void ServerTeleport(Vector3 localPosition, Quaternion localRotation)
        {
            // To prevent applying the position updates received from client (if they have ClientAuth) while being teleported.
            // clientAuthorityBeforeTeleport defaults to false when not teleporting, if it is true then it means that teleport
            // was previously called but not finished therefore we should keep it as true so that 2nd teleport call doesn't clear authority
            clientAuthorityBeforeTeleport = clientAuthority || clientAuthorityBeforeTeleport;
            clientAuthority = false;

            DoTeleport(localPosition, localRotation);

            // tell all clients about new values
            RpcTeleport(localPosition, Compression.CompressQuaternion(localRotation), clientAuthorityBeforeTeleport);
        }

        void DoTeleport(Vector3 newLocalPosition, Quaternion newLocalRotation)
        {
            targetTransform.localPosition = newLocalPosition;
            targetTransform.localRotation = newLocalRotation;

            // Since we are overriding the position we don't need a goal and start.
            // Reset them to null for fresh start
            goal = new DataPoint();
            start = new DataPoint();
            lastPosition = newLocalPosition;
            lastRotation = newLocalRotation;
        }

        [ClientRpc(channel = Channels.DefaultUnreliable)]
        void RpcTeleport(Vector3 newPosition, uint newPackedRotation, bool isClientAuthority)
        {
            DoTeleport(newPosition, Compression.DecompressQuaternion(newPackedRotation));

            // only send finished if is owner and is ClientAuthority on server 
            if (hasAuthority && isClientAuthority)
                CmdTeleportFinished();
        }

        /// <summary>
        /// This RPC will be invoked on server after client finishes overriding the position.
        /// </summary>
        /// <param name="initialAuthority"></param>
        [Command(channel = Channels.DefaultUnreliable)]
        void CmdTeleportFinished()
        {
            if (clientAuthorityBeforeTeleport)
            {
                clientAuthority = true;

                // reset value so doesnt effect future calls, see note in ServerTeleport
                clientAuthorityBeforeTeleport = false;
            }
            else
            {
                Debug.LogWarning("Client called TeleportFinished when clientAuthority was false on server", this);
            }
        }

        #endregion

        #region Debug Gizmos

        // draw the data points for easier debugging
        void OnDrawGizmos()
        {
            // draw start and goal points and a line between them
            if (start.localPosition != goal.localPosition)
            {
                DrawDataPointGizmo(start, Color.yellow);
                DrawDataPointGizmo(goal, Color.green);
                DrawLineBetweenDataPoints(start, goal, Color.cyan);
            }
        }

        static void DrawDataPointGizmo(DataPoint data, Color color)
        {
            // use a little offset because transform.localPosition might be in the ground in many cases
            Vector3 offset = Vector3.up * 0.01f;

            // draw position
            Gizmos.color = color;
            Gizmos.DrawSphere(data.localPosition + offset, 0.5f);

            // draw forward and up like unity move tool
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(data.localPosition + offset, data.localRotation * Vector3.forward);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(data.localPosition + offset, data.localRotation * Vector3.up);
        }

        static void DrawLineBetweenDataPoints(DataPoint data1, DataPoint data2, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(data1.localPosition, data2.localPosition);
        }

        #endregion
    }
}
