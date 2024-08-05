using UnityEngine;
using Mirror;

namespace Mirror.Examples.CouchCoop
{
    public class MovingPlatform : NetworkBehaviour
    {
        public Transform endTarget;
        public float moveSpeed = 0.5f;
        // allows for on demand syncing of stopping and starting platform movement, change via server
        // note,sync vars changed via inspector do not sync. This is optional feature, can be removed
        [SyncVar]
        public bool moveObj = true;

        // optional fancy features
        public bool moveStopsUponExit = false;
        public bool moveStartsUponCollision = false;

        private Vector3 startPosition;
        private Vector3 endPosition;

        void Awake()
        {
            startPosition = transform.position;
            endPosition = endTarget.position;
        }

        void Update()
        {
            if (moveObj)
            {
                float step = moveSpeed * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, endPosition, step);

                if (Vector3.Distance(transform.position, endPosition) < 0.001f)
                {
                    endPosition = endPosition == startPosition ? endTarget.position : startPosition;
                    if (isServer)
                    {
                        RpcResyncPosition(endPosition == startPosition ? (byte)1 : (byte)0);
                    }
                }
            }
        }

        [ClientRpc]
        void RpcResyncPosition(byte _value)
        {
            //print("RpcResyncPosition: " + _value);
            transform.position = _value == 1 ? endTarget.position : startPosition;
        }

        // optional
        [ServerCallback]
        private void OnCollisionEnter(Collision collision)
        {
            if (moveStartsUponCollision)
            {
                if (collision.gameObject.tag == "Player")
                {
                    moveObj = true;
                }
            }
        }

        // optional
        [ServerCallback]
        private void OnCollisionExit(Collision collision)
        {
            if (moveStopsUponExit)
            {
                if (collision.gameObject.tag == "Player")
                {
                    moveObj = false;
                }
            }
        }
    }
}