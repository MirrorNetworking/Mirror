using UnityEngine;
using Mirror;
using System.Collections;

namespace Mirror.Examples.ServerAuthoritativePlayerMovement
{
    public class PlayerMovement : NetworkBehaviour
    {
        //this class should be used on a networked player gameobject, and is intended to be used in a non-server-based setup i.e. server is not a player
        //will mostly work if using server in host mode but not designed for use that way, and some movement on the server itself is intentionally 
        //straight to final desired position rather than smooth movement
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

        void Start()
        {
            //attach camera to the player
            MainCamera = FindObjectOfType<Camera>();
            rigidBody = GetComponent<Rigidbody>();

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

            //this bit must be done outside of isLocalPlayer check to pick up changes for other players
            //if running in host mode (server is also a player) the movement and rotation has already happened so don't do again here
            if (!isServer)
            {
                transform.position = Vector3.MoveTowards(transform.position, ClientMoveToPosition, Time.smoothDeltaTime * MoveSpeed);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, ClientMoveToRotation, Time.smoothDeltaTime * RotateSpeed);
            }

            if (isLocalPlayer)
            {
                if (Time.time - lastSentTime > (1f / PosUpdatesPerSecond)) //don't send too many player position updates per second to the server 
                {
                    //(all movement must be processed by server)

                    //left stick moves in the direction the stick points to rather than in relation to where the player is facing
                    float leftStickHorizontal = Input.GetAxis("Horizontal");
                    float leftStickVertical = Input.GetAxis("Vertical");

                    //right stick turns the player to face the direction the stick is pointing
                    float rightStickHorizontal = Input.GetAxis("Right Joystick Horizontal");
                    float rightStickVertical = Input.GetAxis("Right Joystick Vertical");
                    Quaternion playerDirection = new Quaternion(0, 0, 0, 1);

                    //set direction to face direction player is travelling
                    Vector3 movement = new Vector3(leftStickHorizontal, 0.0f, leftStickVertical);
                    if (movement == Vector3.zero)
                    {
                        //use last known client rotation, otherwise the player will always point upwards
                        playerDirection = ClientMoveToRotation;
                    }
                    else
                    {
                        playerDirection = Quaternion.LookRotation(movement);
                        playerDirection = playerDirection.normalized;
                    }

                    //override the direction if the right stick is being used
                    if (rightStickHorizontal != 0.0 || rightStickVertical != 0.0)
                    {
                        //angle will match where the right stick points to
                        float angle = Mathf.Atan2(rightStickVertical, rightStickHorizontal) * Mathf.Rad2Deg;
                        playerDirection = Quaternion.AngleAxis(180.0f - angle, Vector3.up);                        
                    }
      
                    CmdPlayerMove(leftStickHorizontal, leftStickVertical, playerDirection, Time.smoothDeltaTime);
      
                    lastSentTime = Time.time;
                }
            }
        }

        private void FixedUpdate()
        {
            if (isServer)
            {
                //if running the server as a host, you will get instant movement rather than smooth movement but this script is intended for non-host servers
                //useful for quick testing in host mode however

                //don't move transform via transform.position, move the rigidbody instead.  This prevents overlaps when objects get too close
                //originally when using transform.position, this code block was at the top of OnUpdate() but you should do rigid body updates in FixedUpdate()
                //transform.position = Vector3.MoveTowards(transform.position, ServerMoveToPosition, Time.deltaTime * MoveSpeed);
                //transform.rotation = Quaternion.RotateTowards(transform.rotation, ServerMoveToRotation, Time.deltaTime * RotateSpeed);

                //originally tested using rigidbody MovePosition and using Vector3.MoveTowards with Time.deltaTime but this was a bit clunky
                //rigidBody.MovePosition(Vector3.MoveTowards(transform.position, ServerMoveToPosition, Time.deltaTime * MoveSpeed));
                //rigidBody.MoveRotation(Quaternion.RotateTowards(transform.rotation, ServerMoveToRotation, Time.deltaTime * RotateSpeed));

                //rigidbody MovePosition already handles smooth interpolation so it is better to just enter the final server position
                //remember this code is written with the intention that the server it not a host i.e. the server is dedicated
                //rigidBody.MovePosition(ServerMoveToPosition);
                rigidBody.MoveRotation(ServerMoveToRotation);

                //try using force instead
                rigidBody.AddForce(ServerLastHorizontal * MoveSpeed, 0, ServerLastVertical * MoveSpeed);
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

        void SetCameraPosition()
        {
            if (!MainCamera) return;

            //player cam sits above the individual player's gameobject, no more complicated than this
            Vector3 goalPos = transform.position;
            goalPos.y = MainCamera.transform.position.y;
            MainCamera.transform.position = goalPos;
        }

        [Command]
        void CmdPlayerMove(float axisHorizontal, float axisVertical, Quaternion playerDirection, float clientDeltaTime)
        {
            //movement is true server authoritative, rotation is set by client
            ServerLastHorizontal = axisHorizontal;
            ServerLastVertical = axisVertical;
            ServerMoveToRotation = playerDirection;            
        }
    }
}
