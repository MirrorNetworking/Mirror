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

        // set position carefully depending on the target component
        void ApplyPositionRotationScale(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // local position/rotation for VR support
            targetComponent.localPosition = position;
            targetComponent.localRotation = rotation;
            targetComponent.localScale = scale;
        }

        // local authority client sends sync message to server for broadcasting
        [Command(channel = Channels.Unreliable)]
        void CmdClientToServerSync(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            // apply if in client authority mode
            if (clientAuthority)
                ApplyPositionRotationScale(localPosition, localRotation, localScale);
        }

        // server broadcasts sync message to all clients
        [ClientRpc(channel = Channels.Unreliable)]
        void RpcServerToClientSync(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            // apply for all objects except local player with authority
            if (!IsClientWithAuthority)
                ApplyPositionRotationScale(localPosition, localRotation, localScale);
        }

        void Update()
        {
            // if server then always sync to others.
            if (isServer)
            {
                // check only each 'sendInterval'
                if (Time.time >= lastServerSendTime + sendInterval)
                {
                    // broadcast to clients
                    RpcServerToClientSync(targetComponent.localPosition,
                                          targetComponent.localRotation,
                                          targetComponent.localScale);
                    lastServerSendTime = Time.time;
                }
            }
            // 'else if' because host mode shouldn't send anything to server.
            // it is the server. don't overwrite anything there.
            else if (isClient && IsClientWithAuthority)
            {
                // check only each 'sendInterval'
                if (Time.time >= lastClientSendTime + sendInterval)
                {
                    // send to server
                    CmdClientToServerSync(targetComponent.localPosition,
                                          targetComponent.localRotation,
                                          targetComponent.localScale);
                    lastClientSendTime = Time.time;
                }
            }
        }
    }
}
