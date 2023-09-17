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

        // keep a history of inputs with timestamp
        public int inputHistorySize = 64;
        readonly SortedList<double, PlayerInput> inputs = new SortedList<double, PlayerInput>();

        void Awake()
        {
            // find the white ball once
            whiteBall = FindObjectOfType<WhiteBallPredicted>();
        }

        // called when the local player dragged the white ball.
        // we reuse the white ball's OnMouseDrag and forward the event to here.
        public void OnDraggedBall(Vector3 force)
        {
            // record the input for reconciliation if needed
            if (inputs.Count >= inputHistorySize) inputs.RemoveAt(0);
            inputs.Add(NetworkTime.time, new PlayerInput(NetworkTime.time, force));
            Debug.Log($"Inputs.Count={inputs.Count}");

            // apply locally immediately
            whiteBall.GetComponent<Rigidbody>().AddForce(force);

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

            // TODO consider applying this in FixedUpdate later
            whiteBall.GetComponent<Rigidbody>().AddForce(force);
        }
    }
}
