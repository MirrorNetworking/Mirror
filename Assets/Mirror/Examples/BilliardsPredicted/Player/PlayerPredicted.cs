using UnityEngine;

namespace Mirror.Examples.BilliardsPredicted
{
    public class PlayerPredicted : NetworkBehaviour
    {
        // white ball component
        WhiteBallPredicted whiteBall;

        void Awake()
        {
            // find the white ball once
            whiteBall = FindObjectOfType<WhiteBallPredicted>();
        }

        // called when the local player dragged the white ball.
        // we reuse the white ball's OnMouseDrag and forward the event to here.
        public void OnDraggedBall(Vector3 force)
        {
            // apply locally immediately
            whiteBall.GetComponent<Rigidbody>().AddForce(force);

            // apply on server as well.
            // not necessary in host mode, otherwise we would apply it twice.
            if (!isServer) CmdApplyForce(force);
        }

        // TODO send over unreliable with ack, notify, etc. later
        [Command]
        void CmdApplyForce(Vector3 force)
        {
            whiteBall.GetComponent<Rigidbody>().AddForce(force);
        }
    }
}
