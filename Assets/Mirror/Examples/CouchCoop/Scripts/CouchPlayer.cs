using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.CouchCoop
{
    public class CouchPlayer : NetworkBehaviour
    {
        public Rigidbody rb;
        public float movementSpeed = 3;
        public float jumpSpeed = 6;
        private float movementVelocity;
        private bool isGrounded;

        public CouchPlayerManager couchPlayerManager;
        private KeyCode jumpKey = KeyCode.Space; // Check CouchPlayerManager for controls
        private KeyCode leftKey = KeyCode.LeftArrow;
        private KeyCode rightKey = KeyCode.RightArrow;

        [SyncVar(hook = nameof(OnNumberChangedHook))]
        public int playerNumber = 0;
        public Text textPlayerNumber;

        // a list of players, is used for camera
        public readonly static List<GameObject> playersList = new List<GameObject>();

        public void Start()
        {
            playersList.Add(this.gameObject);
           // print("playersList: " + playersList.Count);

            SetPlayerUI();
        }

        public void OnDestroy()
        {
            playersList.Remove(this.gameObject);
           // print("playersList: " + playersList.Count);
        }

        public override void OnStartAuthority()
        {
            this.enabled = true;

            if (isOwned)
            {
#if UNITY_2022_2_OR_NEWER
                couchPlayerManager = GameObject.FindAnyObjectByType<CouchPlayerManager>();
#else
                // Deprecated in Unity 2023.1
                couchPlayerManager = GameObject.FindObjectOfType<CouchPlayerManager>();
#endif
                // setup controls according to the pre-sets on CouchPlayerManager
                jumpKey = couchPlayerManager.playerKeyJump[playerNumber];
                leftKey = couchPlayerManager.playerKeyLeft[playerNumber];
                rightKey = couchPlayerManager.playerKeyRight[playerNumber];
            }
        }

        void Update()
        {
            if (!Application.isFocused) return;
            if (isOwned == false) { return; }

            // you can control all local players via arrow keys and space bar for fun testing
            // otherwise check and set individual controls in CouchPlayerManager script.
            if (isGrounded == true)
            {
                if (Input.GetKey(KeyCode.Space) || Input.GetKeyDown(jumpKey))
                {
                    rb.velocity = new Vector2(rb.velocity.x, jumpSpeed);
                }
            }

            movementVelocity = 0;

            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(leftKey))
            {
                movementVelocity = -movementSpeed;
            }
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(rightKey))
            {
                movementVelocity = movementSpeed;
            }

            rb.velocity = new Vector2(movementVelocity, rb.velocity.y);
        }

        [ClientCallback]
        void OnCollisionExit(Collision col)
        {
            if (isOwned == false) { return; }
            isGrounded = false;
        }

        [ClientCallback]
        void OnCollisionStay(Collision col)
        {
            if (isOwned == false) { return; }
            isGrounded = true;
        }

        void OnNumberChangedHook(int _old, int _new)
        {
            //Debug.Log(name + " - OnNumberChangedHook: " + playerNumber);
            SetPlayerUI();
        }

        public void SetPlayerUI()
        {
            // called from hook and in start, to solve a race condition
            if (isOwned)
            {
                textPlayerNumber.text = "Local: " + playerNumber;
            }
            else
            {
                textPlayerNumber.text = "Remote: " + playerNumber;
            }
        }
    }
}
