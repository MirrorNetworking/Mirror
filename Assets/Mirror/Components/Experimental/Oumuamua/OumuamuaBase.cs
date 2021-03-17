// based on Glenn Fielder https://gafferongames.com/post/snapshot_interpolation/
//
// Base class for NetworkTransform and NetworkTransformChild.
// => simple unreliable sync without any interpolation for now.
// => which means we don't need teleport detection either
using UnityEngine;

namespace Mirror.Experimental
{
    public abstract class OumuamuaBase : NetworkBehaviour
    {
        [Header("Authority")]
        [Tooltip("Set to true if moves come from owner client, set to false if moves always come from server")]
        public bool clientAuthority;

        // Is this a client with authority over this transform?
        // This component could be on the player object or any object that has been assigned authority to this client.
        bool IsClientWithAuthority => hasAuthority && clientAuthority;

        // target transform to sync. can be on a child.
        protected abstract Transform targetComponent { get; }

        // send interval: send frequently (unreliable, no interpolation)
        [Range(0, 1)] public float sendInterval = 0.050f;
        float lastClientSendTime;
        float lastServerSendTime;

        // snapshot timestamps are _remote_ time
        // we need to interpolate and calculate buffer lifetimes based on it.
        // -> we don't know remote's current time
        // -> NetworkTime.time fluctuates too much, that's no good
        // -> we _could_ calculate an offset when the first snapshot arrives,
        //    but if there was high latency then we'll always calculate time
        //    with high latency
        // -> at any given time, we are interpolating from snapshot A to B
        // => seems like A.timestamp += deltaTime is a good way to do it
        // => let's store it in two variables:
        float serverRemoteClientTime;
        float clientRemoteServerTime;

        // "Experimentally I’ve found that the amount of delay that works best
        //  at 2-5% packet loss is 3X the packet send rate"
        [Tooltip("Snapshots are buffered for sendInterval * multiplier seconds. At 2-5% packet loss, 3x supposedly works best.")]
        public int bufferTimeMultiplier = 3;
        public float bufferTime => sendInterval * bufferTimeMultiplier;

        // snapshot buffers
        SnapshotBuffer serverBuffer = new SnapshotBuffer();
        SnapshotBuffer clientBuffer = new SnapshotBuffer();

        // local authority client sends sync message to server for broadcasting
        [Command(channel = Channels.Unreliable)]
        void CmdClientToServerSync(Snapshot snapshot)
        {
            // apply if in client authority mode
            if (clientAuthority)
            {
                // add to buffer (or drop if older than first element)
                serverBuffer.InsertIfNewEnough(snapshot);
            }
        }

        // server broadcasts sync message to all clients
        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientSync(Snapshot snapshot)
        {
            // apply for all objects except local player with authority
            if (!IsClientWithAuthority)
            {
                // add to buffer (or drop if older than first element)
                clientBuffer.InsertIfNewEnough(snapshot);
            }
        }

        // set position carefully depending on the target component
        void ApplySnapshot(Snapshot snapshot)
        {
            // local position/rotation for VR support
            targetComponent.localPosition = snapshot.position;
            targetComponent.localRotation = snapshot.rotation;
            targetComponent.localScale = snapshot.scale;
        }

        // helper function to apply snapshots.
        // we use the same one on server and client.
        // => called every Update() depending on authority.
        void ApplySnapshots(ref float remoteTime, SnapshotBuffer buffer)
        {
            Debug.Log($"{name} snapshotbuffer={buffer.Count}");

            // we buffer snapshots for 'bufferTime'
            // for example:
            //   * we buffer for 3 x sendInterval = 300ms
            //   * the idea is to wait long enough so we at least have a few
            //     snapshots to interpolate between
            //   * we process anything older 100ms immediately
            //
            // IMPORTANT: snapshot timestamps are _remote_ time
            // we need to interpolate and calculate buffer lifetimes based on it.
            // -> we don't know remote's current time
            // -> NetworkTime.time fluctuates too much, that's no good
            // -> we _could_ calculate an offset when the first snapshot arrives,
            //    but if there was high latency then we'll always calculate time
            //    with high latency
            // -> at any given time, we are interpolating from snapshot A to B
            // => seems like A.timestamp += deltaTime is a good way to do it

            // if remote time wasn't initialized yet
            if (remoteTime == 0)
            {
                // then set it to first snapshot received (if any)
                if (buffer.Peek(out Snapshot first))
                {
                    remoteTime = first.timestamp;
                    Debug.LogWarning("remoteTime initialized to " + first.timestamp);
                }
                // otherwise wait for the first one
                else return;
            }

            // move remote time along deltaTime
            // TODO consider double for precision over days
            // (probably need to speed this up based on buffer size later)
            remoteTime += Time.deltaTime;

            if (buffer.DequeueIfOldEnough(remoteTime, bufferTime, out Snapshot snapshot))
                ApplySnapshot(snapshot);
        }

        void Update()
        {
            // if server then always sync to others.
            if (isServer)
            {
                // broadcast to all clients each 'sendInterval'
                // (client with authority will drop the rpc)
                if (Time.time >= lastServerSendTime + sendInterval)
                {
                    Snapshot snapshot = new Snapshot(
                        Time.time,
                        targetComponent.localPosition,
                        targetComponent.localRotation,
                        targetComponent.localScale
                    );

                    RpcServerToClientSync(snapshot);
                    lastServerSendTime = Time.time;
                }

                // apply buffered snapshots IF client authority
                // -> in server authority, server moves the object
                //    so no need to apply any snapshots there.
                // -> don't apply for host mode player either, even if in
                //    client authority mode. if it doesn't go over the network,
                //    then we don't need to do anything.
                if (clientAuthority && !isLocalPlayer)
                {
                    // apply snapshots
                    ApplySnapshots(ref serverRemoteClientTime, serverBuffer);
                }
            }
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient)
            {
                // client authority, and local player (= allowed to move myself)?
                if (IsClientWithAuthority)
                {
                    // send to server each 'sendInterval'
                    if (Time.time >= lastClientSendTime + sendInterval)
                    {
                        Snapshot snapshot = new Snapshot(
                            Time.time,
                            targetComponent.localPosition,
                            targetComponent.localRotation,
                            targetComponent.localScale
                        );

                        CmdClientToServerSync(snapshot);
                        lastClientSendTime = Time.time;
                    }
                }
                // for all other clients (and for local player if !authority),
                // we need to apply snapshots from the buffer
                else
                {
                    // apply snapshots
                    ApplySnapshots(ref clientRemoteServerTime, clientBuffer);
                }
            }
        }

        void Reset()
        {
            // disabled objects aren't updated anymore.
            // so let's clear the buffers.
            serverBuffer.Clear();
            clientBuffer.Clear();

            // and reset remoteTime so it's initialized to first snapshot again
            clientRemoteServerTime = 0;
            serverRemoteClientTime = 0;
        }

        void OnDisable() => Reset();
        void OnEnable() => Reset();
    }
}
