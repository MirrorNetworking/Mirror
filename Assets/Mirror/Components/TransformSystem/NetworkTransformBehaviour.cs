using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror.TransformSyncing
{
    public class NetworkTransformBehaviour : NetworkBehaviour, IHasPosition
    {
        static readonly ILogger logger = LogFactory.GetLogger<NetworkTransformBehaviour>(LogType.Error);

        bool _needsUpdate;
        float _nextSyncInterval;

        Vector3 IHasPosition.Position => target.localPosition;
        uint IHasPosition.Id => netId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IHasPosition.NeedsUpdate(float now)
        {
            if (_needsUpdate && now > _nextSyncInterval)
            {
                _nextSyncInterval = now + syncInterval;
                return true;
            }
            else
            {
                return false;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IHasPosition.ClearNeedsUpdate()
        {
            _needsUpdate = false;
        }

        void IHasPosition.SetPositionClient(Vector3 position)
        {
            if (!IsClientWithAuthority)
            {
                DeserializeFromReader(position);
            }
        }

        void IHasPosition.SetPositionServer(Vector3 position)
        {
            target.localPosition = position;
            _needsUpdate = true;
        }


        void SendMessageToServer()
        {
            connectionToServer.Send(new NetworkPositionSingleMessage
            {
                id = netId,
                position = target.localPosition,
            });
        }


        public Transform target;

        private void OnValidate()
        {
            if (target == null)
                target = transform;
        }

        public override void OnStartClient()
        {
            NetworkTransformSystem.Instance.AddBehaviour(this);
        }
        public override void OnStartServer()
        {
            NetworkTransformSystem.Instance.AddBehaviour(this);
        }

        public override void OnStopClient()
        {
            NetworkTransformSystem.Instance.RemoveBehaviour(this);
        }
        public override void OnStopServer()
        {
            NetworkTransformSystem.Instance.RemoveBehaviour(this);
        }

        private void OnDestroy()
        {
            // make sure is removed
            NetworkTransformSystem.Instance.RemoveBehaviour(this);
        }

        [Header("Authority")]
        [Tooltip("Set to true if moves come from owner client, set to false if moves always come from server")]
        public bool clientAuthority;

        /// <summary>
        /// We need to store this locally on the server so clients can't request Authority when ever they like
        /// </summary>
        bool clientAuthorityBeforeTeleport;

        // Is this a client with authority over this target?
        // This component could be on the player object or any object that has been assigned authority to this client.
        bool IsClientWithAuthority => hasAuthority && clientAuthority;


        // Sensitivity is added for VR where human players tend to have micro movements so this can quiet down
        // the network traffic.  Additionally, rigidbody drift should send less traffic, e.g very slow sliding / rolling.
        [Header("Sensitivity")]
        [Tooltip("Changes to the target must exceed these values to be transmitted on the network.")]
        public float localPositionSensitivity = .01f;

        // server
        Vector3 lastPosition;

        // client
        public struct DataPoint
        {
            public readonly bool isValid;
            public readonly float timeStamp;
            public readonly Vector3 localPosition;
            public readonly float movementSpeed;

            public DataPoint(float timeStamp, Vector3 localPosition, float movementSpeed)
            {
                isValid = true;
                this.timeStamp = timeStamp;
                this.localPosition = localPosition;
                this.movementSpeed = movementSpeed;
            }
        }
        // interpolation start and goal
        DataPoint start;
        DataPoint goal;

        // local authority send time
        float lastClientSendTime;

        //public override bool OnSerialize(NetworkWriter writer, bool initialState)
        //{
        //    // no syncvars because no base called
        //    writer.WriteVector3(target.localPosition);
        //    return true;
        //}

        // try to estimate movement speed for a data point based on how far it
        // moved since the previous one
        // => if this is the first time ever then we use our best guess:
        //    -> delta based on target.localPosition
        //    -> elapsed based on send interval hoping that it roughly matches
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float EstimateMovementSpeed(DataPoint from, float toTime, Vector3 toPosition)
        {
            float elapsed = GetTimeElapsed(from, toTime);
            // stop NaN error
            if (elapsed <= 0)
            {
                return 0;
            }

            Vector3 fromPosition = from.isValid
                ? from.localPosition
                : target.localPosition;

            Vector3 delta = toPosition - fromPosition;
            return delta.magnitude / elapsed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float GetTimeElapsed(DataPoint from, float toTime)
        {
            return from.isValid
                ? toTime - from.timeStamp
                : syncInterval;
        }

        // serialization is needed by OnSerialize and by manual sending from authority
        void DeserializeFromReader(Vector3 localPosition)
        {
            DataPoint _oldStart = start;
            DataPoint _oldGoal = goal;

            float now = Time.time;
            // movement speed: based on how far it moved since last time
            // has to be calculated before 'start' is overwritten
            float movementSpeed = EstimateMovementSpeed(goal, now, localPosition);

            // put it into a data point immediately
            DataPoint newGoal = new DataPoint(now, localPosition, movementSpeed);

            // reassign start wisely
            // -> first ever data point? then make something up for previous one
            //    so that we can start interpolation without waiting for next.
            if (!start.isValid)
            {
                start = new DataPoint(
                    now - syncInterval,
                    target.localPosition,
                    newGoal.movementSpeed);
            }
            // -> second or nth data point? then update previous, but:
            //    we start at where ever we are right now, so that it's
            //    perfectly smooth and we don't jump anywhere
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
                float newDistance = Vector3.Distance(goal.localPosition, newGoal.localPosition);

                // teleport / lag / obstacle detection: only continue at current
                // position if we aren't too far away
                Vector3 startPos = Vector3.Distance(target.localPosition, start.localPosition) < oldDistance + newDistance
                    ? target.localPosition
                    : goal.localPosition;

                start = new DataPoint(
                    goal.timeStamp,
                    startPos,
                    goal.movementSpeed);
            }

            // set new destination in any case. new data is best data.
            goal = newGoal;
        }

        static Vector3 InterpolatePosition(DataPoint start, DataPoint goal, Vector3 currentPosition)
        {
            if (start.isValid)
            {
                // Option 1: simply interpolate based on time. but stutter
                // will happen, it's not that smooth. especially noticeable if
                // the camera automatically follows the player
                //   float t = CurrentInterpolationFactor();
                //   return Vector3.Lerp(start.position, goal.position, t);

                // Option 2: always += speed
                // -> speed is 0 if we just started after idle, so always use max
                //    for best results
                float speed = Mathf.Max(start.movementSpeed, goal.movementSpeed);
                return Vector3.MoveTowards(currentPosition, goal.localPosition, speed * Time.deltaTime);
            }
            return currentPosition;
        }


        // teleport / lag / stuck detection
        // -> checking distance is not enough since there could be just a tiny
        //    fence between us and the goal
        // -> checking time always works, this way we just teleport if we still
        //    didn't reach the goal after too much time has elapsed
        bool NeedsTeleport()
        {
            // calculate time between the two data points
            float startTime = start.isValid ? start.timeStamp : Time.time - syncInterval;
            float goalTime = goal.isValid ? goal.timeStamp : Time.time;
            float difference = goalTime - startTime;
            float timeSinceGoalReceived = Time.time - goalTime;
            return timeSinceGoalReceived > difference * 5;
        }

        /// <summary>
        /// Has target moved since we last checked
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool HasMoved()
        {
            // moved or rotated or scaled?
            // local position/rotation/scale for VR support
            bool moved = Vector3.Distance(lastPosition, target.localPosition) > localPositionSensitivity;

            // save last for next frame to compare
            // (only if change was detected. otherwise slow moving objects might
            //  never sync because of C#'s float comparison tolerance. see also:
            //  https://github.com/vis2k/Mirror/pull/428)
            if (moved)
            {
                // local position/rotation for VR support
                lastPosition = target.localPosition;
            }
            return moved;
        }

        // set position carefully depending on the target component
        void ApplyPosition(Vector3 position)
        {
            // local position/rotation for VR support
            target.localPosition = position;
        }

        void Update()
        {
            if (isClient)
            {
                ClientUpdate();
            }
        }

        private void ClientUpdate()
        {
            // send to server if we have local authority (and aren't the server)
            // -> only if connectionToServer has been initialized yet too
            if (!isServer && IsClientWithAuthority)
            {
                // check only each 'syncInterval'
                if (Time.time - lastClientSendTime >= syncInterval)
                {
                    if (HasMoved())
                    {
                        SendMessageToServer();
                    }
                    lastClientSendTime = Time.time;
                }
            }

            // apply interpolation on client for all players
            // unless this client has authority over the object. could be
            // himself or another object that he was assigned authority over
            if (!IsClientWithAuthority)
            {
                // received one yet? (initialized?)
                if (goal.isValid)
                {
                    // teleport or interpolate
                    if (NeedsTeleport())
                    {

                        // local position/rotation for VR support
                        ApplyPosition(goal.localPosition);

                        // reset data points so we don't keep interpolating
                        start = default;
                        goal = default;
                    }
                    else
                    {
                        // local position/rotation for VR support
                        ApplyPosition(InterpolatePosition(start, goal, target.localPosition));
                    }
                }
            }
        }


        #region Server Teleport (force move player)
        /// <summary>
        /// Server side teleportation.
        /// This method will override this GameObject's current Transform.Position and Transform.Rotation
        /// to the Vector3 you have provided
        /// and send it to all other Clients to override it at their side too.
        /// </summary>
        /// <param name="position">Where to teleport this GameObject</param>
        /// <param name="rotation">Which rotation to set this GameObject</param>
        [Server]
        public void ServerTeleport(Vector3 position)
        {
            // To prevent applying the position updates received from client (if they have ClientAuth) while being teleported.

            // clientAuthorityBeforeTeleport defaults to false when not teleporting, if it is true then it means that teleport was previously called but not finished
            // therefore we should keep it as true so that 2nd teleport call doesn't clear authority
            clientAuthorityBeforeTeleport = clientAuthority || clientAuthorityBeforeTeleport;
            clientAuthority = false;

            DoTeleport(position);

            // tell all clients about new values
            RpcTeleport(position, clientAuthorityBeforeTeleport);
        }

        void DoTeleport(Vector3 newPosition)
        {
            target.position = newPosition;

            // Since we are overriding the position we don't need a goal and start.
            // Reset them to null for fresh start
            goal = default;
            start = default;
            lastPosition = newPosition;
        }

        [ClientRpc]
        void RpcTeleport(Vector3 newPosition, bool isClientAuthority)
        {
            DoTeleport(newPosition);

            // only send finished if is owner and is ClientAuthority on server
            if (hasAuthority && isClientAuthority)
                CmdTeleportFinished();
        }

        /// <summary>
        /// This RPC will be invoked on server after client finishes overriding the position.
        /// </summary>
        /// <param name="initialAuthority"></param>
        [Command]
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

#if UNITY_EDITOR
        static void DrawDataPointGizmo(DataPoint data, Color color)
        {
            // use a little offset because target.localPosition might be in
            // the ground in many cases
            Vector3 offset = Vector3.up * 0.01f;

            // draw position
            Gizmos.color = color;
            Gizmos.DrawSphere(data.localPosition + offset, 0.5f);

            // draw forward and up
            // like unity move tool
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(data.localPosition + offset, Vector3.forward);

            // like unity move tool
            Gizmos.color = Color.green;
            Gizmos.DrawRay(data.localPosition + offset, Vector3.up);
        }

        static void DrawLineBetweenDataPoints(DataPoint data1, DataPoint data2, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(data1.localPosition, data2.localPosition);
        }

        // draw the data points for easier debugging
        void OnDrawGizmos()
        {
            // draw start and goal points
            if (start.isValid) DrawDataPointGizmo(start, Color.gray);
            if (goal.isValid) DrawDataPointGizmo(goal, Color.white);

            // draw line between them
            if (start.isValid && goal.isValid) DrawLineBetweenDataPoints(start, goal, Color.cyan);
        }
#endif
    }
}
