using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Mirror;

namespace Mirror.Examples.TopDownShooter
{
    public class EnemyTopDown : NetworkBehaviour
    {
        private CanvasTopDown canvasTopDown;

        public float followDistance = 8f; // Distance at which the enemy will start following the target
        public float findPlayersTime = 1.0f; // We want to avoid this being in Update, allow enemies to scan for playes every X time
        public float distanceToKillAt = 0.5f;

        private NavMeshAgent agent;
        private Transform closestTarget;
        public Vector3 previousPosition;

        public GameObject enemyArt;
        public GameObject idleSprite, aggroSprite;
        public AudioSource soundDeath, soundAggro;

        void Awake()
        {
            //allow all to run this, they may need it for reference
#if UNITY_2022_2_OR_NEWER
            canvasTopDown = GameObject.FindAnyObjectByType<CanvasTopDown>();
#else
            canvasTopDown = GameObject.FindObjectOfType<CanvasTopDown>();
#endif
        }

        void Start()
        {
            previousPosition = this.transform.position;

            if (isServer)
            {
                agent = GetComponent<NavMeshAgent>();
                InvokeRepeating("FindClosestTarget", findPlayersTime, findPlayersTime);
            }
#if !UNITY_SERVER
            if (isClient)
            {
                InvokeRepeating("SetSprite", 0.1f, 0.1f);
            }
#endif
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

            // This is our static player list, set and updated in players scripts via Start and OnDestroy.
            foreach (PlayerTopDown target in PlayerTopDown.playerList)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
                if (target.flashLightStatus == true)
                {
                    // players with flashlight off, gets lower aggro by enemies
                    distanceToTarget = distanceToTarget / 2;
                }

                // chase only if alive
                if (target.playerStatus == 0 && distanceToTarget < closestDistance && distanceToTarget <= followDistance)
                {
                    closestDistance = distanceToTarget;
                    closestTarget = target.transform;

                    float distanceKill = Vector3.Distance(transform.position, target.transform.position);
                    if (distanceKill < distanceToKillAt)
                    {
                        target.Kill();
                    }
                }
            }

            // Even with no target, Unitys nav agent continues moving to last set position
            // We do not want this for a respawning enemy, so we manually stop the agent.
            if (closestTarget == null)
            {
                agent.isStopped = true;
            }
            else
            {
                agent.isStopped = false;
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

        [ServerCallback]
        public void Kill()
        {
            RpcKill();
            // Player host will run the RPC, but Server-Only will not, and we need the function to run that the rpc calls, so check and call it.
            if (isServerOnly)
            {
                StartCoroutine(KillCoroutine());
            }
        }

        [ClientRpc]
        void RpcKill()
        {
            StartCoroutine(KillCoroutine());
        }

        IEnumerator KillCoroutine()
        {
#if !UNITY_SERVER
            soundDeath.Play();
            enemyArt.SetActive(false);
            if (isClient)
            {
                GameObject splatter = Instantiate(canvasTopDown.deathSplatter, this.transform.position, this.transform.rotation);
                Destroy(splatter, 5.0f);
            }
#endif
            yield return new WaitForSeconds(0.1f);

            if (isServer)
            {
                // reset enemy, rather than despawning, makes it look like a new enemy appears, better for performance too
                closestTarget = null;
                transform.position = new Vector3(Random.Range(canvasTopDown.networkTopDown.enemySpawnRangeX.x, canvasTopDown.networkTopDown.enemySpawnRangeX.y), 0, Random.Range(canvasTopDown.networkTopDown.enemySpawnRangeZ.x, canvasTopDown.networkTopDown.enemySpawnRangeZ.y));
            }

            yield return new WaitForSeconds(0.1f);
#if !UNITY_SERVER
            enemyArt.SetActive(true);
#endif
            if (isServer)
            {
                // spawn another, this means for every 1 enemy killed, 2 more appear, increasing difficulty
                canvasTopDown.networkTopDown.SpawnEnemy();
            }
        }

        [ClientCallback]
        void SetSprite()
        {
#if !UNITY_SERVER
            // A simple way to change sprite animation, without networking it
            // If not moving, be idle sprite, if moving, presume aggrod sprite.
            if (this.transform.position == previousPosition)
            {
                if (idleSprite.activeInHierarchy == false)
                {
                    idleSprite.SetActive(true);
                    aggroSprite.SetActive(false);
                }
            }
            else
            {
                if (aggroSprite.activeInHierarchy == false)
                {
                    idleSprite.SetActive(false);
                    aggroSprite.SetActive(true);
                    soundAggro.Play();
                }
                previousPosition = this.transform.position;
            }
#endif
        }
    }
}