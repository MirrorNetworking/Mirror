using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Component that controls visibility of networked objects for players.
    /// <para>Any object with this component on it will not be visible to players more than a (configurable) distance away.</para>
    /// </summary>
    [AddComponentMenu("Network/NetworkProximityChecker")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.com/xmldocs/articles/Components/NetworkProximityChecker.html")]
    public class NetworkProximityChecker : NetworkBehaviour
    {
        /// <summary>
        /// Enumeration of methods to use to check proximity.
        /// </summary>
        public enum CheckMethod
        {
            Physics3D,
            Physics2D
        }

        /// <summary>
        /// The maximim range that objects will be visible at.
        /// </summary>
        [Tooltip("The maximum range that objects will be visible at.")]
        public int visRange = 10;

        /// <summary>
        /// How often (in seconds) that this object should update the list of observers that can see it.
        /// </summary>
        [Tooltip("How often (in seconds) that this object should update the list of observers that can see it.")]
        public float visUpdateInterval = 1;

        /// <summary>
        /// Which method to use for checking proximity of players.
        /// <para>Physics3D uses 3D physics to determine proximity.</para>
        /// <para>Physics2D uses 2D physics to determine proximity.</para>
        /// </summary>
        [Tooltip("Which method to use for checking proximity of players.\n\nPhysics3D uses 3D physics to determine proximity.\nPhysics2D uses 2D physics to determine proximity.")]
        public CheckMethod checkMethod = CheckMethod.Physics3D;

        /// <summary>
        /// Flag to force this object to be hidden for players.
        /// <para>If this object is a player object, it will not be hidden for that player.</para>
        /// </summary>
        [Tooltip("Enable to force this object to be hidden from players.")]
        public bool forceHidden;

        // Layers are used anyway, might as well expose them to the user.
        /// <summary>
        /// Select only the Player's layer to avoid unnecessary SphereCasts against the Terrain, etc.
        /// <para>~0 means 'Everything'.</para>
        /// </summary>
        [Tooltip("Select only the Player's layer to avoid unnecessary SphereCasts against the Terrain, etc.")]
        public LayerMask castLayers = ~0;

        float lastUpdateTime;

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

            if (Time.time - lastUpdateTime > visUpdateInterval)
            {
                netIdentity.RebuildObservers(false);
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Called when a new player enters
        /// </summary>
        /// <param name="newObserver"></param>
        /// <returns></returns>
        public override bool OnCheckObserver(NetworkConnection newObserver)
        {
            if (forceHidden)
                return false;

            return Vector3.Distance(newObserver.identity.transform.position, transform.position) < visRange;
        }

        /// <summary>
        /// Called when a new player enters, and when scene changes occur
        /// </summary>
        /// <param name="observers">List of players to be updated.  Modify this set with all the players that can see this object</param>
        /// <param name="initial">True if this is the first time the method is called for this object</param>
        /// <returns>True if this component calculated the list of observers</returns>
        public override bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initial)
        {
            // if force hidden then return without adding any observers.
            if (forceHidden)
                // always return true when overwriting OnRebuildObservers so that
                // Mirror knows not to use the built in rebuild method.
                return true;

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

        /// <summary>
        /// Called when hiding and showing objects on the host
        /// </summary>
        /// <param name="visible"></param>
        public override void OnSetLocalVisibility(bool visible)
        {
            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
            {
                rend.enabled = visible;
            }
        }
    }
}
