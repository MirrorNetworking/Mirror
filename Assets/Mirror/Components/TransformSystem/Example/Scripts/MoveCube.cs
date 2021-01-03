using UnityEngine;
using UnityEngine.AI;

namespace Mirror.TransformSyncing.Example
{
    /// <summary>
    /// Sets NavMesh destination for cube. The server will then sync the position to the clients using NetworkTransformBehaviour
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MoveCube : MonoBehaviour
    {
        [SerializeField] Vector3 min;
        [SerializeField] Vector3 max;
        private NavMeshAgent navMeshAgent;

        private void Awake()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            navMeshAgent.enabled = NetworkServer.active;
        }


        [ServerCallback]
        void Update()
        {
            // if close to destination, set new destination
            if (Vector3.Distance(transform.position, navMeshAgent.destination) < 1f)
            {
                navMeshAgent.destination = RandomPointInBounds();
            }
        }

        public Vector3 RandomPointInBounds()
        {
            return new Vector3(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                Random.Range(min.z, max.z)
            );
        }
    }
}
