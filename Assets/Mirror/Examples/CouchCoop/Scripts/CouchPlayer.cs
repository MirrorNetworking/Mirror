using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.CouchCoop
{
    public class CouchPlayer : NetworkBehaviour
    {
        public Rigidbody rb;
        public float movementSpeed;
        public float jumpSpeed;
        private float movementVelocity;
        private bool isGrounded;

        public CouchPlayerManager couchPlayerManager;
        public KeyCode jumpKey = KeyCode.Space;
        public KeyCode leftKey = KeyCode.A;
        public KeyCode rightKey = KeyCode.D;

        [SyncVar(hook = nameof(OnNumberChangedHook))]
        public int playerNumber = 0;
        public Text textPlayerNumber;

        public override void OnStartAuthority()
        {
            this.enabled = true;

            if (isOwned)
            {
                couchPlayerManager = GameObject.FindObjectOfType<CouchPlayerManager>();
                // setup controls according to the pre-sets on CouchPlayerManager
                jumpKey = couchPlayerManager.playerKeyJump[playerNumber];
                leftKey = couchPlayerManager.playerKeyLeft[playerNumber];
                rightKey = couchPlayerManager.playerKeyRight[playerNumber];
            }
        }

        public void Start()
        {
            SetPlayerUI();
        }

        void Update()
        {
            if (!Application.isFocused) return;
            if (isOwned == false) { return; }

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
