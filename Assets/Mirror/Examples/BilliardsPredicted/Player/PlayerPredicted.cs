using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.BilliardsPredicted
{
    // example input for this exact demo.
    // not general purpose yet.
    public struct PlayerInput
    {
        public double timestamp;
        public Vector3 force;

        public PlayerInput(double timestamp, Vector3 force)
        {
            this.timestamp = timestamp;
            this.force = force;
        }
    }

    public class PlayerPredicted : NetworkBehaviour
    {
        // white ball component
        WhiteBallPredicted whiteBall;

        void Awake()
        {
            // find the white ball once
#if UNITY_2022_2_OR_NEWER
            whiteBall = FindAnyObjectByType<WhiteBallPredicted>();
#else
            // Deprecated in Unity 2023.1
            whiteBall = FindObjectOfType<WhiteBallPredicted>();
#endif
        }

        // apply force to white ball.
        // common function to ensure we apply it the same way on server & client!
        void ApplyForceToWhite(Vector3 force)
        {
            // https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Rigidbody.AddForce.html
            // this is buffered until the next FixedUpdate.

            // get the white ball's Rigidbody.
            // prediction sometimes moves this out of the object for a while,
            // so we need to grab it this way:
            Rigidbody rb = whiteBall.GetComponent<PredictedRigidbody>().predictedRigidbody;

            // AddForce has different force modes, see this excellent diagram:
            // https://www.reddit.com/r/Unity3D/comments/psukm1/know_the_difference_between_forcemodes_a_little/
            // for prediction it's extremely important(!) to apply the correct mode:
            //   'Force' makes server & client drift significantly here
            //   'Impulse' is correct usage with significantly less drift
            rb.AddForce(force, ForceMode.Impulse);
        }

        // called when the local player dragged the white ball.
        // we reuse the white ball's OnMouseDrag and forward the event to here.
        public void OnDraggedBall(Vector3 force)
        {
            // apply force locally immediately
            ApplyForceToWhite(force);

            // apply on server as well.
            // not necessary in host mode, otherwise we would apply it twice.
            if (!isServer) CmdApplyForce(force);
        }

        // while prediction is applied on clients immediately,
        // we still want to validate every input on the server and reject it if necessary.
        // this way we can latency free yet cheat safe movement.
        // this should include a certain tolerance so players aren't hard corrected
        // for their local movement all the time.
        // TODO this should be on some kind of base class for reuse, but perhaps independent of parameters?
        bool IsValidMove(Vector3 force) => true;

        // TODO send over unreliable with ack, notify, etc. later
        [Command]
        void CmdApplyForce(Vector3 force)
        {
            if (!IsValidMove(force))
            {
                Debug.Log($"Server rejected move: {force}");
                return;
            }

            // apply force
            ApplyForceToWhite(force);
        }
    }
}
