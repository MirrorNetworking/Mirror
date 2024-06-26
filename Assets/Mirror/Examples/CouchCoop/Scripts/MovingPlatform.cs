using UnityEngine;
using Mirror;

public class MovingPlatform : NetworkBehaviour
{
    public Transform endTarget; 
    public float moveSpeed = 0.5f;
    // note, disabling this on server, wont automatically disable on clients
    // could be a sync var, incase you do not want constantly moving platform
    public bool moveObj = true; 

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
}