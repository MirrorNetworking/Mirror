using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Mirror;

public class EnemyTopDown : NetworkBehaviour
{
    private CanvasTopDown canvasTopDown;

    public float followDistance = 8f; // Distance at which the enemy will start following the target
    public float findPlayersTime = 1.0f; // we do not want this in Update, allow enemies to scan for playes every X time

    private NavMeshAgent agent;
    private Transform closestTarget;

    void Awake()
    {
        //allow all players to run this, they may need it for reference
        canvasTopDown = GameObject.FindObjectOfType<CanvasTopDown>();
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        InvokeRepeating("FindClosestTarget", findPlayersTime, findPlayersTime);
    }

    [ServerCallback]
    void Update()
    {
        FollowTarget();
    }

    [ServerCallback]
    void FindClosestTarget()
    {
        float closestDistance = Mathf.Infinity;
        closestTarget = null;

        foreach (PlayerTopDown target in PlayerTopDown.playerList)
        {
            float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
            if (target.flashLightStatus == true)
            {
                // players with flashlight off, gets lower aggro by enemies
                distanceToTarget = distanceToTarget / 2;
            }

            if (distanceToTarget < closestDistance && distanceToTarget <= followDistance)
            {
                closestDistance = distanceToTarget;
                closestTarget = target.transform;
            }
        }
    }

    [ServerCallback]
    void FollowTarget()
    {
        if (closestTarget != null)
        {
            agent.SetDestination(closestTarget.position);
        }
    }

    public void Kill()
    {
        // reset enemy, rather than despawning, makes it look like a new enemy appears, better for performance too
        closestTarget = null;
        transform.position = new Vector3(Random.Range(canvasTopDown.networkTopDown.enemySpawnRangeX.x, canvasTopDown.networkTopDown.enemySpawnRangeX.y), 0, Random.Range(canvasTopDown.networkTopDown.enemySpawnRangeZ.x, canvasTopDown.networkTopDown.enemySpawnRangeZ.y));

        // spawn another, this means for every 1 enemy killed, 2 more appear, increasing difficulty
        canvasTopDown.networkTopDown.SpawnEnemy();
    }
}
