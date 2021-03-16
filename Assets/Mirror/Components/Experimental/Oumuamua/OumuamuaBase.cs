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

        // set position carefully depending on the target component
        void ApplySnapshot(Snapshot snapshot)
        {
            // local position/rotation for VR support
            targetComponent.localPosition = snapshot.position;
            targetComponent.localRotation = snapshot.rotation;
            targetComponent.localScale = snapshot.scale;
        }

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
                    ApplySnapshot(snapshot);
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
                    ApplySnapshot(snapshot);
                    clientReceivedSequence = snapshot.sequence;
                }
            }
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
            }
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient && IsClientWithAuthority)
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
        }
    }
}
