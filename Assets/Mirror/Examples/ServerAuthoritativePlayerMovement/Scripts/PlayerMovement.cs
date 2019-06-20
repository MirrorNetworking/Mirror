using UnityEngine;
using Mirror;
using System.Collections;

namespace Mirror.Examples.ServerAuthoritativePlayerMovement
{
    public class PlayerMovement : NetworkBehaviour
    {
        //this class should be used on a networked player gameobject, and is intended to be used in a non-server-based setup i.e. server is not a player
        //will mostly work if using server in host mode but not designed for use that way, and some movement on the server itself is intentionally 
        //straight to final desired position rather than smooth movement.  Also if server as a host, host is always ahead of all other players
        private float lastSentTime;
        public float PosUpdatesPerSecond = 20;
        public float MoveSpeed = 50;
        public float RotateSpeed = 500;

        public Camera MainCamera;

        //these need to be synced so each client knows where the other clients are moving to
        //(each client needs to move all player objects to where the server tells them to move to, not just their own)
        //this also includes the player object for the client running the game - you don't move your own player object, the server does
        [SyncVar]
        Vector3 ClientMoveToPosition;
        [SyncVar]
        Quaternion ClientMoveToRotation;

        //no need to sync this as only server uses it    
        Vector3 ServerMoveToPosition = Vector3.zero;
        Quaternion ServerMoveToRotation = new Quaternion(0, 0, 0, 1);
        float ServerLastHorizontal, ServerLastVertical;
        Rigidbody rigidBody; //this is removed on non-servers and is used for server movement, hence why is declared at class level
        bool IsRightStickConfigured;

        void Start()
        {
            //attach camera to the player
            MainCamera = FindObjectOfType<Camera>();
            rigidBody = GetComponent<Rigidbody>();

            IsRightStickConfigured = (IsAxisAvailable("Right Joystick Horizontal") && IsAxisAvailable("Right Joystick Horizontal"));
            if (!IsRightStickConfigured)
                Debug.Log("Check the \\Mirror\\Examples\\ServerAuthoritativePlayerMovement\\Right Joystick Inputs Image folder for example setup details.");

            StartCoroutine(AfterStart());
        }

        IEnumerator AfterStart()
        {
            //returning 0 will make it wait 1 frame so can determine if running on server or not
            yield return 0;

            //Wait until the server is active so can tell if this instance is running on server or client
            while (!NetworkServer.active)
            {
                yield return null; //Must wait here to ensure server is active
            }

            if (isServer)
            {
                ServerMoveToPosition = transform.position;
                ServerMoveToRotation = transform.rotation;
                ServerMoveToRotation = ServerMoveToRotation.normalized;
            }

            if (isLocalPlayer)
            {
                ClientMoveToPosition = transform.position;
                ClientMoveToRotation = transform.rotation;
                if (!isServer) //server needs to keep rigid body, only want physics to be done on server
                {
                    //your choice here, do one or the other depending if you need a rigidbody for animation etc.
                    Destroy(rigidBody);
                    //rigidBody.isKinematic = true;
                }

                SetCameraPosition();
            }
        }

        void Update()
        {

            //UpdatePlayerPositionOnClient must be called outside of the isLocalPlayer code block to pick up changes for all other players
            //if running in host mode (server is also a player) the movement and rotation has already happened so don't do again here
            if (!isServer)
            {
                UpdatePlayerPositionOnClient();
            }

            if (isLocalPlayer)
            {
                if (Time.time - lastSentTime > (1f / PosUpdatesPerSecond)) //don't send too many player position updates per second to the server 
                {
                    //(all movement must be processed by server)                    
                    Vector2 playerMoveDirection = GetInputMovement();

                    //right stick might not be set up
                    Quaternion playerRotateDirection;
                    if (IsRightStickConfigured)
                        playerRotateDirection = GetInputRotation(playerMoveDirection);
                    else
                        playerRotateDirection = Quaternion.identity;

                    CmdPlayerMove(playerMoveDirection, playerRotateDirection, Time.smoothDeltaTime);

                    lastSentTime = Time.time;
                }
            }
        }

        private void FixedUpdate()
        {
            if (isServer)
            {
                rigidBody.AddForce(ServerLastHorizontal * MoveSpeed, 0, ServerLastVertical * MoveSpeed);
                //need to move smoothly rather than jumping server to final angle, in case fire in direction while rotating the player
                rigidBody.MoveRotation(Quaternion.RotateTowards(transform.rotation, ServerMoveToRotation, Time.deltaTime * RotateSpeed));
            }
        }

        void LateUpdate() //after physics stuff is done
        {
            //need to update position of this gameobject to new location provided by server
            if (isServer)
            {
                ClientMoveToPosition = rigidBody.position;
                ClientMoveToRotation = rigidBody.rotation;
            }

            if (isLocalPlayer)
                SetCameraPosition();
        }

        Vector2 GetInputMovement()
        {
            //left stick moves in the direction the stick points to rather than in relation to where the player is facing
            float moveStickHorizontal = Input.GetAxis("Horizontal");
            float moveStickVertical = Input.GetAxis("Vertical");
            return new Vector2(moveStickHorizontal, moveStickVertical);
        }

        Quaternion GetInputRotation(Vector2 PlayerMoveDirection)
        {
            //right stick turns the player to face the direction the stick is pointing (make sure to set up the Right Joystick inputs)            
            float directionStickHorizontal = Input.GetAxis("Right Joystick Horizontal");
            float directionStickVertical = Input.GetAxis("Right Joystick Vertical");

            Quaternion playerRotateDirection = new Quaternion(0, 0, 0, 1);

            //set direction to face direction player is travelling
            //(x is horizontal input, y is vertical input but as camera is above player y maps to Vector3.z)
            Vector3 movement = new Vector3(PlayerMoveDirection.x, 0.0f, PlayerMoveDirection.y);
            if (movement == Vector3.zero)
            {
                //use last known client rotation, otherwise the player will always point upwards when not pointing right stick
                playerRotateDirection = ClientMoveToRotation;
            }
            else
            {
                playerRotateDirection = Quaternion.LookRotation(movement);
                playerRotateDirection = playerRotateDirection.normalized;
            }

            //override the direction if the right stick is being used
            if (directionStickHorizontal != 0.0 || directionStickVertical != 0.0)
            {
                //angle will match where the right stick points to
                float angle = Mathf.Atan2(directionStickVertical, directionStickHorizontal) * Mathf.Rad2Deg;
                playerRotateDirection = Quaternion.AngleAxis(180.0f - angle, Vector3.up);
            }

            return playerRotateDirection;
        }

        void UpdatePlayerPositionOnClient()
        {
            transform.position = Vector3.MoveTowards(transform.position, ClientMoveToPosition, Time.smoothDeltaTime * MoveSpeed);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, ClientMoveToRotation, Time.smoothDeltaTime * RotateSpeed);
        }

        void SetCameraPosition()
        {
            if (!MainCamera) return;

            //player cam sits above the individual player's gameobject, no more complicated than this
            Vector3 goalPos = transform.position;
            goalPos.y = MainCamera.transform.position.y;
            MainCamera.transform.position = goalPos;
        }

        [Command]
        void CmdPlayerMove(Vector2 playerMoveDirection, Quaternion playerRotateDirection, float clientDeltaTime)
        {
            //movement is true server authoritative using rigidbody and force, rotation is set by client but the server will rotate towards the chosen direction
            //and push out the current server's rotation to all clients to follow the server's lead
            ServerLastHorizontal = playerMoveDirection.x;
            ServerLastVertical = playerMoveDirection.y;
            ServerMoveToRotation = playerRotateDirection;
        }

        bool IsAxisAvailable(string axisName)
        {
            try
            {
                Input.GetAxis(axisName);
                return true;
            }
            catch (System.Exception /*exc*/)
            {
                Debug.LogFormat("Axis {0} is not defined.  Check your project settings, Input tab to set this up.", axisName);
                return false;
            }
        }
    }
}
