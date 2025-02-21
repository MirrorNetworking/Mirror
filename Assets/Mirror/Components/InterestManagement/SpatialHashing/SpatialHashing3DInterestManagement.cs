// extremely fast spatial hashing interest management based on uMMORPG GridChecker.
// => 30x faster in initial tests
// => scales way higher
// checks on three dimensions (XYZ) which includes the vertical axes.
// this is slower than XY checking for regular spatial hashing.
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/ Interest Management/ Spatial Hash/Spatial Hashing Interest Management")]
    public class SpatialHashing3DInterestManagement : InterestManagement
    {
        [Tooltip("The maximum range that objects will be visible.\nSet to 10-20% larger than camera far clip plane")]
        public int visRange = 1200;

        // we use a 9 neighbour grid.
        // so we always see in a distance of 2 grids.
        // for example, our own grid and then one on top / below / left / right.
        //
        // this means that grid resolution needs to be distance / 2.
        // so for example, for distance = 1200 we see 2 cells = 600 * 2 distance.
        //
        // on first sight, it seems we need distance / 3 (we see left/us/right).
        // but that's not the case.
        // resolution would be 400, and we only see 1 cell far, so 400+400=800.
        public int resolution => visRange / 2; // same as XY because if XY is rotated 90 degree for 3D, it's still the same distance

        [Tooltip("Rebuild all every 'rebuildInterval' seconds.")]
        public float rebuildInterval = 1;
        double lastRebuildTime;

        [Header("Debug Settings")]
        public bool showSlider;

        // the grid
        // begin with a large capacity to avoid resizing & allocations.
        Grid3D<NetworkConnectionToClient> grid = new Grid3D<NetworkConnectionToClient>(1024);

        // project 3d world position to grid position
        Vector3Int ProjectToGrid(Vector3 position) =>
            Vector3Int.RoundToInt(position / resolution);

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
        {
            // calculate projected positions
            Vector3Int projected = ProjectToGrid(identity.transform.position);
            Vector3Int observerProjected = ProjectToGrid(newObserver.identity.transform.position);

            // distance needs to be at max one of the 8 neighbors, which is
            //   1 for the direct neighbors
            //   1.41 for the diagonal neighbors (= sqrt(2))
            // => use sqrMagnitude and '2' to avoid computations. same result.
            return (projected - observerProjected).sqrMagnitude <= 2; // same as XY because if XY is rotated 90 degree for 3D, it's still the same distance
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
        {
            // add everyone in 9 neighbour grid
            // -> pass observers to GetWithNeighbours directly to avoid allocations
            //    and expensive .UnionWith computations.
            Vector3Int current = ProjectToGrid(identity.transform.position);
            grid.GetWithNeighbours(current, newObservers);
        }

        [ServerCallback]
        public override void ResetState()
        {
            lastRebuildTime = 0D;
        }

        // update everyone's position in the grid
        // (internal so we can update from tests)
        [ServerCallback]
        internal void Update()
        {
            // NOTE: unlike Scene/MatchInterestManagement, this rebuilds ALL
            //       entities every INTERVAL. consider the other approach later.

            // Old Notes: refresh grid every update!
            // => newly spawned entities get observers assigned via
            //    OnCheckObservers. this can happen any time and we don't want
            //    them broadcast to old (moved or destroyed) connections.
            // => players do move all the time. we want them to always be in the
            //    correct grid position.
            // => note that the actual 'rebuildall' doesn't need to happen all
            //    the time.

            // Updated Notes: refresh grid and RebuildAll every interval.
            // Real world application would have visRange larger than camera
            // far clip plane, e.g. 1200, and a player movement speed of ~10m/sec
            // so they typically won't cross cell boundaries quickly. If users notice
            // flickering or mis-positioning, they can decrease the interval.

            // clear old grid results before we update everyone's position.
            // (this way we get rid of destroyed connections automatically)
            //
            // NOTE: keeps allocated HashSets internally.
            //       clearing & populating every frame works without allocations

            // rebuild all spawned entities' observers every 'interval'
            // this will call OnRebuildObservers which then returns the
            // observers at grid[position] for each entity.
            if (NetworkTime.localTime >= lastRebuildTime + rebuildInterval)
            {
                RefreshGrid();
                RebuildAll();
                lastRebuildTime = NetworkTime.localTime;
            }
        }

        // (internal so we can update from tests)
        internal void RefreshGrid()
        {
            grid.ClearNonAlloc();

            // put every connection into the grid at it's main player's position
            // NOTE: player sees in a radius around him. NOT around his pet too.
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                // authenticated and joined world with a player?
                if (connection.isAuthenticated && connection.identity != null)
                {
                    // calculate current grid position
                    Vector3Int position = ProjectToGrid(connection.identity.transform.position);

                    // put into grid
                    grid.Add(position, connection);
                }
            }
        }

        // OnGUI allocates even if it does nothing. avoid in release.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // slider from dotsnet. it's nice to play around with in the benchmark
        // demo.
        void OnGUI()
        {
            if (!showSlider) return;

            // only show while server is running. not on client, etc.
            if (!NetworkServer.active) return;

            int height = 30;
            int width = 250;
            GUILayout.BeginArea(new Rect(Screen.width / 2 - width / 2, Screen.height - height, width, height));
            GUILayout.BeginHorizontal("Box");
            GUILayout.Label("Radius:");
            visRange = Mathf.RoundToInt(GUILayout.HorizontalSlider(visRange, 0, 200, GUILayout.Width(150)));
            GUILayout.Label(visRange.ToString());
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
#endif
    }
}
