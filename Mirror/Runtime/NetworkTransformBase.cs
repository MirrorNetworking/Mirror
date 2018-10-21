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

namespace Mirror
{
    public abstract class NetworkTransformBase : NetworkBehaviour
    {
        [Range(0, 1)] public float sendInterval = 0.1f; // every ... seconds

        // rotation compression. not public so that other scripts can't modify
        // it at runtime. alternatively we could send 1 extra byte for the mode
        // each time so clients know how to decompress, but the whole point was
        // to save bandwidth in the first place.
        // -> can still be modified in the Inspector while the game is running,
        //    but would cause errors immediately and be pretty obvious.
        [Tooltip("Compresses 16 Byte Quaternion into None=12, Some=6, Much=3, Lots=2 Byte")]
        [SerializeField] Compression compressRotation = Compression.Much;
        public enum Compression { None, Some, Much, Lots }; // easily understandable and funny

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

        // one GameObject might have multiple NetworkTransform/Child components. We need to know the index to properly
        // assign the MsgType.LocalPlayerTransform message.
        int componentIndex;

        public override float GetNetworkSendInterval()
        {
            return sendInterval;
        }

        void Awake()
        {
            // TODO use componentId instead?
            componentIndex = Array.IndexOf(GetComponents<NetworkTransformBase>(), this);
        }

        // ScaleFloatToUShort( -1f, -1f, 1f, ushort.MinValue, ushort.MaxValue) => 0
        // ScaleFloatToUShort(  0f, -1f, 1f, ushort.MinValue, ushort.MaxValue) => 32767
        // ScaleFloatToUShort(0.5f, -1f, 1f, ushort.MinValue, ushort.MaxValue) => 49151
        // ScaleFloatToUShort(  1f, -1f, 1f, ushort.MinValue, ushort.MaxValue) => 65535
        static ushort ScaleFloatToUShort(float value, float minValue, float maxValue, ushort minTarget, ushort maxTarget)
        {
            // note: C# ushort - ushort => int, hence so many casts
            int targetRange = maxTarget - minTarget; // max ushort - min ushort > max ushort. needs bigger type.
            float valueRange = maxValue - minValue;
            float valueRelative = value - minValue;
            return (ushort)(minTarget + (ushort)(valueRelative/valueRange * (float)targetRange));
        }

        // ScaleFloatToByte( -1f, -1f, 1f, byte.MinValue, byte.MaxValue) => 0
        // ScaleFloatToByte(  0f, -1f, 1f, byte.MinValue, byte.MaxValue) => 127
        // ScaleFloatToByte(0.5f, -1f, 1f, byte.MinValue, byte.MaxValue) => 191
        // ScaleFloatToByte(  1f, -1f, 1f, byte.MinValue, byte.MaxValue) => 255
        static byte ScaleFloatToByte(float value, float minValue, float maxValue, byte minTarget, byte maxTarget)
        {
            // note: C# byte - byte => int, hence so many casts
            int targetRange = maxTarget - minTarget; // max byte - min byte only fits into something bigger
            float valueRange = maxValue - minValue;
            float valueRelative = value - minValue;
            return (byte)(minTarget + (byte)(valueRelative/valueRange * (float)targetRange));
        }

        // ScaleUShortToFloat(    0, ushort.MinValue, ushort.MaxValue, -1, 1) => -1
        // ScaleUShortToFloat(32767, ushort.MinValue, ushort.MaxValue, -1, 1) => 0
        // ScaleUShortToFloat(49151, ushort.MinValue, ushort.MaxValue, -1, 1) => 0.4999924
        // ScaleUShortToFloat(65535, ushort.MinValue, ushort.MaxValue, -1, 1) => 1
        static float ScaleUShortToFloat(ushort value, ushort minValue, ushort maxValue, float minTarget, float maxTarget)
        {
            // note: C# ushort - ushort => int, hence so many casts
            float targetRange = maxTarget - minTarget;
            ushort valueRange = (ushort)(maxValue - minValue);
            ushort valueRelative = (ushort)(value - minValue);
            return minTarget + (float)((float)valueRelative/(float)valueRange * targetRange);
        }

        // ScaleByteToFloat(  0, byte.MinValue, byte.MaxValue, -1, 1) => -1
        // ScaleByteToFloat(127, byte.MinValue, byte.MaxValue, -1, 1) => -0.003921569
        // ScaleByteToFloat(191, byte.MinValue, byte.MaxValue, -1, 1) => 0.4980392
        // ScaleByteToFloat(255, byte.MinValue, byte.MaxValue, -1, 1) => 1
        static float ScaleByteToFloat(byte value, byte minValue, byte maxValue, float minTarget, float maxTarget)
        {
            // note: C# byte - byte => int, hence so many casts
            float targetRange = maxTarget - minTarget;
            byte valueRange = (byte)(maxValue - minValue);
            byte valueRelative = (byte)(value - minValue);
            return minTarget + ((float)valueRelative/(float)valueRange * targetRange);
        }

        // eulerAngles have 3 floats, putting them into 2 bytes of [x,y],[z,0]
        // would be a waste. instead we compress into 5 bits each => 15 bits.
        // so a ushort.
        static ushort PackThreeFloatsIntoUShort(float u, float v, float w, float minValue, float maxValue)
        {
            // 5 bits max value = 1+2+4+8+16 = 31 = 0x1F
            byte lower = ScaleFloatToByte(u, minValue, maxValue, 0x00, 0x1F);
            byte middle = ScaleFloatToByte(v, minValue, maxValue, 0x00, 0x1F);
            byte upper = ScaleFloatToByte(w, minValue, maxValue, 0x00, 0x1F);
            ushort combined = (ushort)(upper << 10 | middle << 5 | lower);
            return combined;
        }

        // see PackThreeFloatsIntoUShort for explanation
        static float[] UnpackUShortIntoThreeFloats(ushort combined, float minTarget, float maxTarget)
        {
            byte lower = (byte)(combined & 0x1F);
            byte middle = (byte)((combined >> 5) & 0x1F);
            byte upper = (byte)(combined >> 10); // nothing on the left, no & needed

            // note: we have to use 4 bits per float, so between 0x00 and 0x0F
            float u = ScaleByteToFloat(lower, 0x00, 0x1F, minTarget, maxTarget);
            float v = ScaleByteToFloat(middle, 0x00, 0x1F, minTarget, maxTarget);
            float w = ScaleByteToFloat(upper, 0x00, 0x1F, minTarget, maxTarget);
            return new float[]{u, v, w};
        }

        // serialization is needed by OnSerialize and by manual sending from authority
        static bool SerializeIntoWriter(NetworkWriter writer, bool initialState, Vector3 position, Quaternion rotation, Compression compressRotation)
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
            else if (compressRotation == Compression.Some)
            {
                // write 3 shorts = 6 byte. scaling [0,360] to [0,65535]
                writer.Write(ScaleFloatToUShort(euler.x, 0, 360, ushort.MinValue, ushort.MaxValue));
                writer.Write(ScaleFloatToUShort(euler.y, 0, 360, ushort.MinValue, ushort.MaxValue));
                writer.Write(ScaleFloatToUShort(euler.z, 0, 360, ushort.MinValue, ushort.MaxValue));
            }
            else if (compressRotation == Compression.Much)
            {
                // write 3 byte. scaling [0,360] to [0,255]
                writer.Write(ScaleFloatToByte(euler.x, 0, 360, byte.MinValue, byte.MaxValue));
                writer.Write(ScaleFloatToByte(euler.y, 0, 360, byte.MinValue, byte.MaxValue));
                writer.Write(ScaleFloatToByte(euler.z, 0, 360, byte.MinValue, byte.MaxValue));
            }
            else if (compressRotation == Compression.Lots)
            {
                // write 2 byte, 5 bits for each float
                writer.Write(PackThreeFloatsIntoUShort(euler.x, euler.y, euler.z, 0, 360));
            }

            return true;
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            return SerializeIntoWriter(writer, initialState, targetComponent.transform.position, targetComponent.transform.rotation, compressRotation);
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
        public void DeserializeFromReader(NetworkReader reader, bool initialState)
        {
            // put it into a data point immediately
            DataPoint temp = new DataPoint();

            // deserialize position
            temp.position = reader.ReadVector3();

            // deserialize rotation
            if (compressRotation == Compression.None)
            {
                // read 3 floats = 16 byte
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();
                temp.rotation = Quaternion.Euler(x, y, z);
            }
            else if (compressRotation == Compression.Some)
            {
                // read 3 shorts = 6 byte. scaling [-32768,32767] to [0,360]
                float x = ScaleUShortToFloat(reader.ReadUInt16(), ushort.MinValue, ushort.MaxValue, 0, 360);
                float y = ScaleUShortToFloat(reader.ReadUInt16(), ushort.MinValue, ushort.MaxValue, 0, 360);
                float z = ScaleUShortToFloat(reader.ReadUInt16(), ushort.MinValue, ushort.MaxValue, 0, 360);
                temp.rotation = Quaternion.Euler(x, y, z);
            }
            else if (compressRotation == Compression.Much)
            {
                // read 3 byte. scaling [0,255] to [0,360]
                float x = ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360);
                float y = ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360);
                float z = ScaleByteToFloat(reader.ReadByte(), byte.MinValue, byte.MaxValue, 0, 360);
                temp.rotation = Quaternion.Euler(x, y, z);
            }
            else if (compressRotation == Compression.Lots)
            {
                // read 2 byte, 5 bits per float
                float[] xyz = UnpackUShortIntoThreeFloats(reader.ReadUInt16(), 0, 360);
                temp.rotation = Quaternion.Euler(xyz[0], xyz[1], xyz[2]);
            }

            // timestamp
            temp.timeStamp = Time.time;

            // movement speed: based on how far it moved since last time
            // has to be calculated before 'start' is overwritten
            temp.movementSpeed = EstimateMovementSpeed(goal, temp, targetComponent.transform, GetNetworkSendInterval());

            // reassign start wisely
            // -> first ever data point? then make something up for previous one
            //    so that we can start interpolation without waiting for next.
            if (start == null)
            {
                start = new DataPoint{
                    timeStamp=Time.time - GetNetworkSendInterval(),
                    position=targetComponent.transform.position,
                    rotation=targetComponent.transform.rotation,
                    movementSpeed=temp.movementSpeed
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
            DeserializeFromReader(reader, initialState);
        }

        // local authority client sends sync message to server for broadcasting
        // note: message is registered in NetworkServer.RegisterMessageHandlers
        //       because internal messages can't be registered from the outside.
        static public void OnClientToServerSync(NetworkMessage netMsg)
        {
            // read message
            TransformMessage message = netMsg.ReadMessage<TransformMessage>();

            // find that gameobject
            GameObject foundObj = NetworkServer.FindLocalObject(message.netId);
            if (foundObj == null)
            {
                if (LogFilter.logError) { Debug.LogError("Received NetworkTransform data for GameObject that doesn't exist"); }
                return;
            }
            NetworkTransformBase[] foundSyncs = foundObj.GetComponents<NetworkTransformBase>();
            if (foundSyncs == null || foundSyncs.Length == 0 || message.componentIndex > foundSyncs.Length - 1)
            {
                if (LogFilter.logError) { Debug.LogError("HandleTransform null target"); }
                return;
            }

            NetworkTransformBase foundSync = foundSyncs[message.componentIndex];
            if (!foundSync.localPlayerAuthority)
            {
                if (LogFilter.logError) { Debug.LogError("HandleTransform no localPlayerAuthority"); }
                return;
            }
            if (netMsg.conn.clientOwnedObjects == null)
            {
                if (LogFilter.logError) { Debug.LogError("HandleTransform object not owned by connection"); }
                return;
            }

            if (netMsg.conn.clientOwnedObjects.Contains(message.netId))
            {
                // deserialize message
                foundSync.DeserializeFromReader(netMsg.reader, true);

                // server-only mode does no interpolation to save computations,
                // but let's set the position directly
                if (foundSync.isServer && !foundSync.isClient)
                    foundSync.ApplyPositionAndRotation(foundSync.goal.position, foundSync.goal.rotation);

                // set dirty so that OnSerialize broadcasts it
                foundSync.SetDirtyBit(1L);
            }
            else
            {
                if (LogFilter.logWarn) { Debug.LogWarning("HandleTransform netId:" + message.netId + " is not for a valid player"); }
            }
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
            float startTime = start != null ? start.timeStamp : Time.time - GetNetworkSendInterval();
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
            lastPosition = targetComponent.transform.position;
            lastRotation = targetComponent.transform.rotation;

            return moved || rotated;
        }

        // set position carefully depending on the target component
        void ApplyPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            targetComponent.transform.position = position;
            targetComponent.transform.rotation = rotation;
        }

        void Update()
        {
            // if server then always sync to others.
            if (isServer)
            {
                // just use OnSerialize via SetDirtyBit only sync when position
                // changed. set dirty bits 0 or 1
                SetDirtyBit((ulong)(HasMovedOrRotated() ? 1L : 0L));
            }

            // no 'else if' since host mode would be both
            if (isClient)
            {
                // send to server if we have local authority (and arent the server)
                // -> only if connectionToServer has been initialized yet too
                if (!isServer && hasAuthority && connectionToServer != null)
                {
                    // check only each 'sendinterval'
                    if (Time.time - lastClientSendTime >= GetNetworkSendInterval())
                    {
                        if (HasMovedOrRotated())
                        {
                            // serialize
                            NetworkWriter writer = new NetworkWriter();
                            SerializeIntoWriter(writer, true, targetComponent.transform.position, targetComponent.transform.rotation, compressRotation);

                            // send message to server
                            TransformMessage message = new TransformMessage();
                            message.netId = netId;
                            message.componentIndex = componentIndex;
                            message.payload = writer.ToArray();
                            connectionToServer.Send((short)MsgType.LocalPlayerTransform, message);
                        }
                        lastClientSendTime = Time.time;
                    }
                }

                // apply interpolation on client for all players
                // except for local player if he has authority and handles it himself
                if (!(isLocalPlayer && hasAuthority))
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

        // draw the data points for easier debugging
        void OnDrawGizmos()
        {
            if (start != null) DrawDataPointGizmo(start, Color.gray);
            if (goal != null) DrawDataPointGizmo(goal, Color.white);
        }
    }
}
