// Base class for NetworkTransform and NetworkTransformChild.
// Simply syncs position/rotation/scale without any interpolation for now.
// (which means we don't need teleport detection either)
using UnityEngine;

namespace Mirror
{
    public abstract class NetworkTransformBase : NetworkBehaviour
    {
        [Header("Authority")]
        [Tooltip("Set to true if moves come from owner client, set to false if moves always come from server")]
        public bool clientAuthority;

        // Is this a client with authority over this transform?
        // This component could be on the player object or any object that has been assigned authority to this client.
        bool IsClientWithAuthority => hasAuthority && clientAuthority;

        // target transform to sync. can be on a child.
        protected abstract Transform targetComponent { get; }

        // local authority send time
        float lastClientSendTime;

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            // serialize position, rotation, scale
            // use local position/rotation/scale for VR support
            // note: we do NOT compress rotation.
            //       we are CPU constrained, not bandwidth constrained.
            //       the code needs to WORK for the next 5-10 years of development.
            writer.WriteVector3(targetComponent.localPosition);
            writer.WriteQuaternion(targetComponent.localRotation);
            writer.WriteVector3(targetComponent.localScale);
            return true;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            // deserialize
            Vector3 localPosition = reader.ReadVector3();
            Quaternion localRotation = reader.ReadQuaternion();
            Vector3 localScale = reader.ReadVector3();

            // apply on client for all players
            // unless this client has authority over the object. could be
            // himself or another object that he was assigned authority over
            if (!IsClientWithAuthority)
            {
                ApplyPositionRotationScale(localPosition, localRotation, localScale);
            }
        }

        // local authority client sends sync message to server for broadcasting
        [Command]
        void CmdClientToServerSync(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            // Ignore messages from client if not in client authority mode
            if (!clientAuthority)
                return;

            // server-only mode does no interpolation to save computations,
            // but let's set the position directly
            if (isServer && !isClient)
                ApplyPositionRotationScale(localPosition, localRotation, localScale);
        }

        // set position carefully depending on the target component
        void ApplyPositionRotationScale(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // local position/rotation for VR support
            targetComponent.transform.localPosition = position;
            targetComponent.transform.localRotation = rotation;
            targetComponent.transform.localScale = scale;
        }

        void Update()
        {
            // if server then always sync to others.
            if (isServer)
            {
                // dirty at all times. sync each syncInterval.
                SetDirtyBit(1UL);
            }

            // no 'else if' since host mode would be both
            if (isClient)
            {
                // send to server if we have local authority (and aren't the server)
                // -> only if connectionToServer has been initialized yet too
                if (!isServer && IsClientWithAuthority)
                {
                    // check only each 'syncInterval'
                    if (Time.time - lastClientSendTime >= syncInterval)
                    {
                        // send to server
                        CmdClientToServerSync(targetComponent.transform.localPosition,
                                              targetComponent.transform.localRotation,
                                              targetComponent.transform.localScale);
                        lastClientSendTime = Time.time;
                    }
                }
            }
        }
    }
}
