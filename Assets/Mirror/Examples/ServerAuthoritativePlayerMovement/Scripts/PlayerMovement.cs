using UnityEngine;
using Mirror;

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
    Vector3 ServerMoveToPosition;    
    Quaternion ServerMoveToRotation;
    float ServerLastHorizontal, ServerLastVertical;
    Rigidbody rigidBody; //this is removed on non-servers and is used for server movement, hence why is declared at class level

    // Start is called before the first frame update
    void Start()
    {
        //attach camera to the player
        MainCamera = FindObjectOfType<Camera>();

        rigidBody = GetComponent<Rigidbody>();

        if (isServer)
        {
            ServerMoveToPosition = transform.position;
            ServerMoveToRotation = transform.rotation;
        }

        if (isLocalPlayer)
        {
            ClientMoveToPosition = transform.position;
            ClientMoveToRotation = transform.rotation;
            if (!isServer) //server needs to keep rigid body, only want physics to be done on server
            {
                Destroy(rigidBody);
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
            transform.position = Vector3.MoveTowards(transform.position, ClientMoveToPosition, Time.deltaTime * MoveSpeed);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, ClientMoveToRotation, Time.deltaTime * RotateSpeed);
        }

        if (isLocalPlayer)
        {
            if (Time.time - lastSentTime > (1f / PosUpdatesPerSecond)) //don't send too many player position updates per second to the server 
            {
                //(all movement must be processed by server)

                //right stick turns the player to face the direction the stick is pointing
                float rightStickHorizontal = Input.GetAxis("Right Joystick Horizontal");
                float rightStickVertical = Input.GetAxis("Right Joystick Vertical");
                if (rightStickHorizontal != 0.0 || rightStickVertical != 0.0)
                {
                    //angle will match where the right stick points to
                    float angle = Mathf.Atan2(rightStickVertical, rightStickHorizontal) * Mathf.Rad2Deg;
                    CmdPlayerRotate(Quaternion.AngleAxis(180.0f - angle, Vector3.up));
                }
                else
                {
                    //send last client rotation to server as final desired rotation when stop pointing with right stick
                    CmdPlayerRotate(transform.rotation);
                }

                //left stick moves in the direction the stick points to rather than in relation to where the player is facing
                float leftStickHorizontal = Input.GetAxis("Horizontal");
                float leftStickVertical = Input.GetAxis("Vertical");
                //if (leftStickHorizontal != 0.0 || leftStickVertical != 0.0)
                {
                    CmdPlayerMove(leftStickHorizontal, leftStickVertical,
                        (rightStickHorizontal == 0.0f && rightStickVertical == 0.0f), Time.smoothDeltaTime);
                }

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
    void CmdPlayerMove(float axisHorizontal, float axisVertical, bool alsoRotate, float clientDeltaTime)
    {
        //can get some jittering if server gets to destination too quickly, so trying to adjust for this by using AddForce
        //ServerMoveToPosition = rigidBody.position + new Vector3(axisHorizontal, 0, axisVertical) * clientDeltaTime * MoveSpeed;
        ServerLastHorizontal = axisHorizontal;
        ServerLastVertical = axisVertical;
        
        //alsoRotate is only true if the client is not using the right stick to point in a direction      
        //this is used to make the player object rotate in the direction it is moving automatically, which seems more "natural"
        if (alsoRotate)
        {
            Vector3 movement = new Vector3(axisHorizontal, 0.0f, axisVertical);
            if (movement != Vector3.zero)
            {
                ServerMoveToRotation = Quaternion.LookRotation(movement);
            }
        }
    }

    [Command]
    void CmdPlayerRotate(Quaternion angle)
    {
        ServerMoveToRotation = angle;
    }

}
