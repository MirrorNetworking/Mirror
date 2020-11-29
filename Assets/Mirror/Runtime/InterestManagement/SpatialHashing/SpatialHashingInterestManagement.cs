// Spatial Hashing based on uMMORPG GridChecker
using UnityEngine;

namespace Mirror
{
    public class SpatialHashingInterestManagement : InterestManagement
    {
        // view range
        // note: unlike uMMORPG, this can now be changed AT RUNTIME because
        // the grid is cleared in every Rebuild :)
        public int visibilityRadius = 100;

        // if we see 8 neighbors then 1 entry is visRange/3
        public int resolution => visibilityRadius / 3;

        // 2D vs 3D
        public enum CheckMethod
        {
            XZ_FOR_3D,
            XY_FOR_2D
        }
        [TooltipAttribute("Which method to use for checking proximity of players.")]
        public CheckMethod checkMethod = CheckMethod.XZ_FOR_3D;

        // the grid
        Grid2D<NetworkConnectionToClient> grid = new Grid2D<NetworkConnectionToClient>();

        // project 3d position to grid
        static Vector2Int ProjectToGrid(Vector3 position, CheckMethod checkMethod, int resolution)
        {
            // simple rounding for now
            // 3D uses xz (horizontal plane)
            // 2D uses xy
            if (checkMethod == CheckMethod.XZ_FOR_3D)
            {
                return Vector2Int.RoundToInt(new Vector2(position.x, position.z) / resolution);
            }
            else
            {
                return Vector2Int.RoundToInt(new Vector2(position.x, position.y) / resolution);
            }
        }

        // rebuild observers and store the result in rebuild buffer
        protected override void RebuildObservers()
        {
            // clear grid
            grid.Clear();

            // put every ready player connection into the grid
            // (observers are always player connections)
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn.isReady && conn.identity != null)
                {
                    Vector2Int gridPosition = ProjectToGrid(conn.identity.transform.position, checkMethod, resolution);
                    grid.Add(gridPosition, conn);
                }
            }

            // for each spawned, assign rebuild observers from grid at position
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                // clear previous rebuild in any case
                identity.rebuild.Clear();

                // only add observers if not currently hidden from observers
                if (!identity.forceHidden)
                {
                    Vector2Int gridPosition = ProjectToGrid(identity.transform.position, checkMethod, resolution);
                    grid.GetWithNeighbours(gridPosition, identity.rebuild);
                }
            }
        }
    }
}
