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

        // "When we send snapshot data in packets, we include at the top a 16 bit
        // sequence number. This sequence number starts at zero and increases
        // with each packet sent. We use this sequence number on receive to
        // determine if the snapshot in a packet is newer or older than the most
        // recent snapshot received. If it’s older then it’s thrown away."
        ushort serverSendSequence;
        ushort clientSendSequence;
        ushort serverReceivedSequence;
        ushort clientReceivedSequence;

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
                // newer than most recent received snapshot?
                if (snapshot.sequence > serverReceivedSequence)
                {
                    // add to buffer
                    serverBuffer.Enqueue(snapshot, Time.time);
                    serverReceivedSequence = snapshot.sequence;
                }
            }
        }

        // server broadcasts sync message to all clients
        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientSync(Snapshot snapshot)
        {
            // apply for all objects except local player with authority
            if (!IsClientWithAuthority)
            {
                // newer than most recent received snapshot?
                if (snapshot.sequence > clientReceivedSequence)
                {
                    // add to buffer
                    clientBuffer.Enqueue(snapshot, Time.time);
                    clientReceivedSequence = snapshot.sequence;
                }
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
        void ApplySnapshots(SnapshotBuffer buffer)
        {
            Debug.Log($"{name} snapshotbuffer={buffer.Count}");

            // we buffer snapshots for 'bufferTime'
            // for example:
            //   * we buffer for 3 x sendInterval = 300ms
            //   * the idea is to wait long enough so we at least have a few
            //     snapshots to interpolate between
            //   * we process anything older 100ms immediately
            if (buffer.DequeueIfOldEnough(Time.time, bufferTime, out Snapshot snapshot))
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
                    ++serverSendSequence;
                    Snapshot snapshot = new Snapshot(
                        serverSendSequence,
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
                    ApplySnapshots(serverBuffer);
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
                        ++clientSendSequence;
                        Snapshot snapshot = new Snapshot(
                            clientSendSequence,
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
                    ApplySnapshots(clientBuffer);
                }
            }
        }

        void OnDisable()
        {
            // disabled objects aren't updated anymore.
            // so let's clear the buffers.
            serverBuffer.Clear();
            clientBuffer.Clear();
        }

        void OnEnable()
        {
            // just in case we received anything while disabled...
            // it's outdated now anyway. clear the buffers.
            serverBuffer.Clear();
            clientBuffer.Clear();
        }
    }
}
