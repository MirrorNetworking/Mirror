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
using UnityEngine;

namespace Mirror
{
    public abstract class NetworkTransformBase : NetworkBehaviour
    {
        // rotation compression. not public so that other scripts can't modify
        // it at runtime. alternatively we could send 1 extra byte for the mode
        // each time so clients know how to decompress, but the whole point was
        // to save bandwidth in the first place.
        // -> can still be modified in the Inspector while the game is running,
        //    but would cause errors immediately and be pretty obvious.
        [Tooltip("Compresses 16 Byte Quaternion into None=12, Much=3, Lots=2 Byte")]
        [SerializeField] Compression compressRotation = Compression.Much;
        public enum Compression { None, Much, Lots , NoRotation }; // easily understandable and funny

        // server
        Vector3 lastPosition;
        Quaternion lastRotation;

        // client
        public class DataPoint
        {
            public float timeStamp;
            public Vector3 position;
            public Quaternion rotation;
            public float movementSpeed;
        }
        // interpolation start and goal
        DataPoint start;
        DataPoint goal;

        // local authority send time
        float lastClientSendTime;

        // target transform to sync. can be on a child.
        protected abstract Transform targetComponent { get; }

        // serialization is needed by OnSerialize and by manual sending from authority
        static void SerializeIntoWriter(NetworkWriter writer, Vector3 position, Quaternion rotation, Compression compressRotation)
        {
            // serialize position
            writer.Write(position);

            // serialize rotation
            // writing quaternion = 16 byte
            // writing euler angles = 12 byte
            // -> quaternion->euler->quaternion always works.
            // -> gimbal lock only occurs when adding.
            Vector3 euler = rotation.eulerAngles;
            if (compressRotation == Compression.None)
            {
                // write 3 floats = 12 byte
                writer.Write(euler.x);
                writer.Write(euler.y);
                writer.Write(euler.z);
            }
            else if (compressRotation == Compression.Much)
            {
                // write 3 byte. scaling [0,360] to [0,255]
                writer.Write(FloatBytePacker.ScaleFloatToByte(euler.x, 0, 360, byte.MinValue, byte.MaxValue));
                writer.Write(FloatBytePacker.ScaleFloatToByte(euler.y, 0, 360, byte.MinValue, byte.MaxValue));
                writer.Write(FloatBytePacker.ScaleFloatToByte(euler.z, 0, 360, byte.MinValue, byte.MaxValue));
            }
            else if (compressRotation == Compression.Lots)
            {
                // write 2 byte, 5 bits for each float
                writer.Write(FloatBytePacker.PackThreeFloatsIntoUShort(euler.x, euler.y, euler.z, 0, 360));
            }
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            SerializeIntoWriter(writer, targetComponent.transform.position, targetComponent.transform.rotation, compressRotation);
            return true;
        }

        // try to estimate movement speed for a data point based on how far it
        // moved since the previous one
        // => if this is the first time ever then we use our best guess:
        //    -> delta based on transform.position
        //    -> elapsed based on send interval hoping that it roughly matches
        static float EstimateMovementSpeed(DataPoint from, DataPoint to, Transform transform, float sendInterval)
        {
            Vector3 delta = to.position - (from != null ? from.position : transform.position);
            float elapsed = from != null ? to.timeStamp - from.timeStamp : sendInterval;
            return elapsed > 0 ? delta.magnitude / elapsed : 0; // avoid NaN
        }

        // serialization is needed by OnSerialize and by manual sending from authority
        void DeserializeFromReader(NetworkReader reader)
        {
            // put it into a data point immediately
            DataPoint temp = new DataPoint
            {
                // deserialize position
                position = reader.ReadVector3()
            };

            // deserialize rotation
            if (compressRotation == Compression.None)
            {
                // read 3 floats = 16 byte
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();
                temp.rotation = Quaternion.Euler(x, y, z);
            }
            else if (compressRotation == Compression.Much)
            {
                // read 3 byte. scaling [0,255] to [0,360]
                float x = FloatBytePacker.ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360);
                float y = FloatBytePacker.ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360);
                float z = FloatBytePacker.ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360);
                temp.rotation = Quaternion.Euler(x, y, z);
            }
            else if (compressRotation == Compression.Lots)
            {
                // read 2 byte, 5 bits per float
                float[] xyz = FloatBytePacker.UnpackUShortIntoThreeFloats(reader.ReadUInt16(), 0, 360);
                temp.rotation = Quaternion.Euler(xyz[0], xyz[1], xyz[2]);
            }

            temp.timeStamp = Time.time;

            // movement speed: based on how far it moved since last time
            // has to be calculated before 'start' is overwritten
            temp.movementSpeed = EstimateMovementSpeed(goal, temp, targetComponent.transform, syncInterval);

            // reassign start wisely
            // -> first ever data point? then make something up for previous one
            //    so that we can start interpolation without waiting for next.
            if (start == null)
            {
                start = new DataPoint{
                    timeStamp = Time.time - syncInterval,
                    position = targetComponent.transform.position,
                    rotation = targetComponent.transform.rotation,
                    movementSpeed = temp.movementSpeed
                };
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
                float oldDistance = Vector3.Distance(start.position, goal.position);
                float newDistance = Vector3.Distance(goal.position, temp.position);

                start = goal;

                // teleport / lag / obstacle detection: only continue at current
                // position if we aren't too far away
                if (Vector3.Distance(targetComponent.transform.position, start.position) < oldDistance + newDistance)
                {
                    start.position = targetComponent.transform.position;
                    start.rotation = targetComponent.transform.rotation;
                }
            }

            // set new destination in any case. new data is best data.
            goal = temp;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // deserialize
            DeserializeFromReader(reader);
        }

        // local authority client sends sync message to server for broadcasting
        [Command]
        void CmdClientToServerSync(byte[] payload)
        {
            // deserialize payload
            NetworkReader reader = new NetworkReader(payload);
            DeserializeFromReader(reader);

            // server-only mode does no interpolation to save computations,
            // but let's set the position directly
            if (isServer && !isClient)
                ApplyPositionAndRotation(goal.position, goal.rotation);

            // set dirty so that OnSerialize broadcasts it
            SetDirtyBit(1UL);
        }

        // where are we in the timeline between start and goal? [0,1]
        static float CurrentInterpolationFactor(DataPoint start, DataPoint goal)
        {
            if (start != null)
            {
                float difference = goal.timeStamp - start.timeStamp;

                // the moment we get 'goal', 'start' is supposed to
                // start, so elapsed time is based on:
                float elapsed = Time.time - goal.timeStamp;
                return difference > 0 ? elapsed / difference : 0; // avoid NaN
            }
            return 0;
        }

        static Vector3 InterpolatePosition(DataPoint start, DataPoint goal, Vector3 currentPosition)
        {
            if (start != null)
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
                return Vector3.MoveTowards(currentPosition, goal.position, speed * Time.deltaTime);
            }
            return currentPosition;
        }

        static Quaternion InterpolateRotation(DataPoint start, DataPoint goal, Quaternion defaultRotation)
        {
            if (start != null)
            {
                float t = CurrentInterpolationFactor(start, goal);
                return Quaternion.Slerp(start.rotation, goal.rotation, t);
            }
            return defaultRotation;
        }

        // teleport / lag / stuck detection
        // -> checking distance is not enough since there could be just a tiny
        //    fence between us and the goal
        // -> checking time always works, this way we just teleport if we still
        //    didn't reach the goal after too much time has elapsed
        bool NeedsTeleport()
        {
            // calculate time between the two data points
            float startTime = start != null ? start.timeStamp : Time.time - syncInterval;
            float goalTime = goal != null ? goal.timeStamp : Time.time;
            float difference = goalTime - startTime;
            float timeSinceGoalReceived = Time.time - goalTime;
            return timeSinceGoalReceived > difference * 5;
        }

        // moved since last time we checked it?
        bool HasMovedOrRotated()
        {
            // moved or rotated?
            bool moved = lastPosition != targetComponent.transform.position;
            bool rotated = lastRotation != targetComponent.transform.rotation;

            // save last for next frame to compare
            // (only if change was detected. otherwise slow moving objects might
            //  never sync because of C#'s float comparison tolerance. see also:
            //  https://github.com/vis2k/Mirror/pull/428)
            bool change = moved || rotated;
            if (change)
            {
                lastPosition = targetComponent.transform.position;
                lastRotation = targetComponent.transform.rotation;
            }
            return change;
        }

        // set position carefully depending on the target component
        void ApplyPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            targetComponent.transform.position = position;
            if (Compression.NoRotation != compressRotation)
            {
                targetComponent.transform.rotation = rotation;
            }
        }

        void Update()
        {
            // if server then always sync to others.
            if (isServer)
            {
                // just use OnSerialize via SetDirtyBit only sync when position
                // changed. set dirty bits 0 or 1
                SetDirtyBit(HasMovedOrRotated() ? 1UL : 0UL);
            }

            // no 'else if' since host mode would be both
            if (isClient)
            {
                // send to server if we have local authority (and aren't the server)
                // -> only if connectionToServer has been initialized yet too
                if (!isServer && hasAuthority)
                {
                    // check only each 'syncInterval'
                    if (Time.time - lastClientSendTime >= syncInterval)
                    {
                        if (HasMovedOrRotated())
                        {
                            // serialize
                            NetworkWriter writer = new NetworkWriter();
                            SerializeIntoWriter(writer, targetComponent.transform.position, targetComponent.transform.rotation, compressRotation);

                            // send to server
                            CmdClientToServerSync(writer.ToArray());
                        }
                        lastClientSendTime = Time.time;
                    }
                }

                // apply interpolation on client for all players
                // unless this client has authority over the object. could be
                // himself or another object that he was assigned authority over
                if (!hasAuthority)
                {
                    // received one yet? (initialized?)
                    if (goal != null)
                    {
                        // teleport or interpolate
                        if (NeedsTeleport())
                        {
                            ApplyPositionAndRotation(goal.position, goal.rotation);
                        }
                        else
                        {
                            ApplyPositionAndRotation(InterpolatePosition(start, goal, targetComponent.transform.position),
                                                     InterpolateRotation(start, goal, targetComponent.transform.rotation));
                        }
                    }
                }
            }
        }

        static void DrawDataPointGizmo(DataPoint data, Color color)
        {
            // use a little offset because transform.position might be in
            // the ground in many cases
            Vector3 offset = Vector3.up * 0.01f;

            // draw position
            Gizmos.color = color;
            Gizmos.DrawSphere(data.position + offset, 0.5f);

            // draw forward and up
            Gizmos.color = Color.blue; // like unity move tool
            Gizmos.DrawRay(data.position + offset, data.rotation * Vector3.forward);

            Gizmos.color = Color.green; // like unity move tool
            Gizmos.DrawRay(data.position + offset, data.rotation * Vector3.up);
        }

        static void DrawLineBetweenDataPoints(DataPoint data1, DataPoint data2, Color color)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(data1.position, data2.position);
        }

        // draw the data points for easier debugging
        void OnDrawGizmos()
        {
            // draw start and goal points
            if (start != null) DrawDataPointGizmo(start, Color.gray);
            if (goal != null) DrawDataPointGizmo(goal, Color.white);

            // draw line between them
            if (start != null && goal != null) DrawLineBetweenDataPoints(start, goal, Color.cyan);
        }
    }
}
