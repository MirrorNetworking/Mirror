using System;
using Mirror;
using UnityEngine;

namespace Mirror.Examples.Billiards
{
    public class WhiteBall : NetworkBehaviour
    {
        public LineRenderer dragIndicator;
        public Rigidbody rigidBody;
        public float forceMultiplier = 2;
        public float maxForce = 40;

        // remember start position to reset to after entering a pocket
        Vector3 startPosition;

        // cast mouse position on screen to world position
        bool MouseToWorld(out Vector3 position)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float distance))
            {
                position = ray.GetPoint(distance);
                return true;
            }
            position = default;
            return false;
        }

        void Awake()
        {
            startPosition = transform.position;
        }

        [ClientCallback]
        void OnMouseDown()
        {
            // enable drag indicator
            dragIndicator.SetPosition(0, transform.position);
            dragIndicator.SetPosition(1, transform.position);
            dragIndicator.gameObject.SetActive(true);
        }

        [ClientCallback]
        void OnMouseDrag()
        {
            // cast mouse position to world
            if (!MouseToWorld(out Vector3 current)) return;

            // drag indicator
            dragIndicator.SetPosition(0, transform.position);
            dragIndicator.SetPosition(1, current);
        }

        // all players can apply force to the white ball.
        // (this is not cheat safe)
        [Command(requiresAuthority = false)]
        void CmdApplyForce(Vector3 force)
        {
            // AddForce has different force modes, see this excellent diagram:
            // https://www.reddit.com/r/Unity3D/comments/psukm1/know_the_difference_between_forcemodes_a_little/
            // when applying a one-time force to the ball, we need 'Impulse'.
            rigidBody.AddForce(force, ForceMode.Impulse);
        }

        [ClientCallback]
        void OnMouseUp()
        {
            // cast mouse position to world
            if (!MouseToWorld(out Vector3 current)) return;

            // calculate delta from ball to mouse
            // ball may have moved since we started dragging,
            // so always use current ball position here.
            Vector3 from = transform.position;

            // debug drawing: only works if Gizmos are enabled!
            Debug.DrawLine(from, current, Color.white, 2);

            // calculate pending force delta
            Vector3 delta = from - current;
            Vector3 force = delta * forceMultiplier;

            // there should be a maximum allowed force
            force = Vector3.ClampMagnitude(force, maxForce);

            // apply force to rigidbody.
            // it will take a round trip to show the effect.
            // the goal for prediction will be to show it immediately.
            CmdApplyForce(force);

            // disable drag indicator
            dragIndicator.gameObject.SetActive(false);
        }

        // reset position when entering a pocket.
        // there's only one trigger in the scene (the pocket).
        [ServerCallback]
        void OnTriggerEnter(Collider other)
        {
            rigidBody.position = startPosition;
            rigidBody.Sleep(); // reset forces
            GetComponent<NetworkRigidbodyUnreliable>().RpcTeleport(startPosition);
        }
    }
}
