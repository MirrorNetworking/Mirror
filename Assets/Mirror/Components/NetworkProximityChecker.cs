using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/NetworkProximityChecker")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkProximityChecker")]
    public class NetworkProximityChecker : NetworkBehaviour
    {
        public enum CheckMethod
        {
            Physics3D,
            Physics2D
        }

        [Tooltip("The maximum range that objects will be visible at.")]
        public int visRange = 10;

        // how often to refresh the list of observers, in seconds
        [Tooltip("How often (in seconds) that this object should update the set of players that can see it.")]
        public float visUpdateInterval = 1;

        [Tooltip("Which method to use for checking proximity of players.\n\nPhysics3D uses 3D physics to determine proximity.\n\nPhysics2D uses 2D physics to determine proximity.")]
        public CheckMethod checkMethod = CheckMethod.Physics3D;

        [Tooltip("Enable to force this object to be hidden from players.")]
        public bool forceHidden;

        // ~0 means 'Everything'. layers are used anyway, might as well expose them to the user.
        [Tooltip("Select only the Player's layer to avoid unnecessary SphereCasts against the Terrain, etc.")]
        public LayerMask castLayers = ~0;

        float m_VisUpdateTime;

        // OverlapSphereNonAlloc array to avoid allocations.
        // -> static so we don't create one per component
        // -> this is worth it because proximity checking happens for just about
        //    every entity on the server!
        // -> should be big enough to work in just about all cases
        static Collider[] hitsBuffer3D = new Collider[10000];
        static Collider2D[] hitsBuffer2D = new Collider2D[10000];

        void Update()
        {
            if (!NetworkServer.active)
                return;

            if (Time.time - m_VisUpdateTime > visUpdateInterval)
            {
                netIdentity.RebuildObservers(false);
                m_VisUpdateTime = Time.time;
            }
        }

        // called when a new player enters
        public override bool OnCheckObserver(NetworkConnection newObserver)
        {
            if (forceHidden)
                return false;

            return Vector3.Distance(newObserver.playerController.transform.position, transform.position) < visRange;
        }

        public override bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initial)
        {
            if (forceHidden)
                return false;

            // find players within range
            switch (checkMethod)
            {
                case CheckMethod.Physics3D:
                {
                    // cast without allocating GC for maximum performance
                    int hitCount = Physics.OverlapSphereNonAlloc(transform.position, visRange, hitsBuffer3D, castLayers);
                    if (hitCount == hitsBuffer3D.Length) Debug.LogWarning("NetworkProximityChecker's OverlapSphere test for " + name + " has filled the whole buffer(" + hitsBuffer3D.Length + "). Some results might have been omitted. Consider increasing buffer size.");

                    for (int i = 0; i < hitCount; i++)
                    {
                        Collider hit = hitsBuffer3D[i];
                        // collider might be on pelvis, often the NetworkIdentity is in a parent
                        // (looks in the object itself and then parents)
                        NetworkIdentity identity = hit.GetComponentInParent<NetworkIdentity>();
                        // (if an object has a connectionToClient, it is a player)
                        if (identity != null && identity.connectionToClient != null)
                        {
                            observers.Add(identity.connectionToClient);
                        }
                    }
                    break;
                }

                case CheckMethod.Physics2D:
                {
                    // cast without allocating GC for maximum performance
                    int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, visRange, hitsBuffer2D, castLayers);
                    if (hitCount == hitsBuffer2D.Length) Debug.LogWarning("NetworkProximityChecker's OverlapCircle test for " + name + " has filled the whole buffer(" + hitsBuffer2D.Length + "). Some results might have been omitted. Consider increasing buffer size.");

                    for (int i = 0; i < hitCount; i++)
                    {
                        Collider2D hit = hitsBuffer2D[i];
                        // collider might be on pelvis, often the NetworkIdentity is in a parent
                        // (looks in the object itself and then parents)
                        NetworkIdentity identity = hit.GetComponentInParent<NetworkIdentity>();
                        // (if an object has a connectionToClient, it is a player)
                        if (identity != null && identity.connectionToClient != null)
                        {
                            observers.Add(identity.connectionToClient);
                        }
                    }
                    break;
                }
            }

            // always return true when overwriting OnRebuildObservers so that
            // Mirror knows not to use the built in rebuild method.
            return true;
        }

        // called hiding and showing objects on the host
        public override void OnSetLocalVisibility(bool visible)
        {
            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
            {
                rend.enabled = visible;
            }
        }
    }
}
