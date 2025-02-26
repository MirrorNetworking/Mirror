using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/ Interest Management/ Spatial Hash/Hex Spatial Hash (3D)")]
    public class HexSpatialHash3DInterestManagement : InterestManagement
    {
        [Range(1, 60), Tooltip("Time interval in seconds between observer rebuilds")]
        public byte rebuildInterval = 1;

        [Range(1, 60), Tooltip("Time interval in seconds between static object rebuilds")]
        public byte staticRebuildInterval = 10;

        [Range(10, 5000), Tooltip("Radius of super hex.\nSet to 10% larger than camera far clip plane.")]
        public ushort visRange = 1100;

        [Range(10, 5000), Tooltip("Cell3D height effects all 3 layers")]
        public ushort cellHeight = 500;

        [Range(1, 100), Tooltip("Distance an object must move for updating cell positions")]
        public ushort minMoveDistance = 1;

        double lastRebuildTime;

        // Counter for batching static object updates
        byte rebuildCounter = 0;

        HexGrid3D grid;

        // Sparse array mapping cell indices to sets of NetworkIdentities
        readonly List<HashSet<NetworkIdentity>> cells = new List<HashSet<NetworkIdentity>>();

        // Tracks the last known cell position and world position of each NetworkIdentity for efficient updates
        readonly Dictionary<NetworkIdentity, (Cell3D cell, Vector3 worldPos)> lastIdentityPositions = new Dictionary<NetworkIdentity, (Cell3D, Vector3)>();

        // Tracks the last known cell position and world position of each player's connection (observer)
        readonly Dictionary<NetworkConnectionToClient, (Cell3D cell, Vector3 worldPos)> lastConnectionPositions = new Dictionary<NetworkConnectionToClient, (Cell3D, Vector3)>();

        // Pre-allocated array for storing neighbor cells (center + 6 neighbors per layer x 3 layers)
        readonly Cell3D[] neighborCells = new Cell3D[21];

        // Maps each connection to the set of NetworkIdentities it can observe, precomputed for rebuilds
        readonly Dictionary<NetworkConnectionToClient, HashSet<NetworkIdentity>> connectionObservers = new Dictionary<NetworkConnectionToClient, HashSet<NetworkIdentity>>();

        // Reusable list for safe iteration over NetworkIdentities, avoiding ToList() allocations
        readonly List<NetworkIdentity> identityKeys = new List<NetworkIdentity>();

        // Pool of reusable HashSet<NetworkIdentity> instances to reduce allocations
        readonly Stack<HashSet<NetworkIdentity>> cellPool = new Stack<HashSet<NetworkIdentity>>();

        // Set of static NetworkIdentities that don't move, updated less frequently
        readonly HashSet<NetworkIdentity> staticObjects = new HashSet<NetworkIdentity>();

        // Scene bounds: ±9 km (18 km total) in each dimension
        const int MAX_Q = 19; // Covers -9 to 9 (~18 km)
        const int MAX_R = 23; // Covers -11 to 11 (~18 km)
        const int LAYER_OFFSET = 18; // Offset for -18 to 17 layers
        const int MAX_LAYERS = 36; // Total layers for ±9 km (18 km)
        const ushort MAX_AREA = 9000; // Maximum area in meters

        void Awake()
        {
            grid = new HexGrid3D(visRange, cellHeight);
            // Initialize cells list with null entries up to max size (±9 km bounds)
            int maxSize = MAX_Q * MAX_R * MAX_LAYERS;
            for (int i = 0; i < maxSize; i++)
                cells.Add(null);
        }

        void LateUpdate()
        {
            if (NetworkTime.time - lastRebuildTime >= rebuildInterval)
            {
                // Update positions of all active connections (players) in the network
                foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
                    if (conn?.identity != null) // Ensure connection and its identity exist
                    {
                        Vector3 position = conn.identity.transform.position;
                        // Only update if the position has changed significantly
                        if (!lastConnectionPositions.TryGetValue(conn, out (Cell3D cell, Vector3 worldPos) last) ||
                            Vector3.Distance(position, last.worldPos) >= minMoveDistance)
                        {
                            Cell3D cell = grid.WorldToCell(position); // Convert world position to grid cell
                            lastConnectionPositions[conn] = (cell, position); // Store the player's cell and position
                        }
                    }

                // Populate the reusable list with current keys for safe iteration
                identityKeys.Clear();
                identityKeys.AddRange(lastIdentityPositions.Keys);

                // Update dynamic objects every rebuild, static objects every staticRebuildInterval
                bool updateStatic = rebuildCounter >= staticRebuildInterval;
                foreach (NetworkIdentity identity in identityKeys)
                    if (updateStatic || !staticObjects.Contains(identity))
                        UpdateIdentityPosition(identity); // Refresh cell position for dynamic or scheduled static objects

                if (updateStatic)
                    rebuildCounter = 0; // Reset the counter after updating static objects
                else
                    rebuildCounter++;

                // Precompute observer sets for each connection before rebuilding
                connectionObservers.Clear();
                foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
                {
                    if (conn?.identity == null || !lastConnectionPositions.TryGetValue(conn, out (Cell3D cell, Vector3 worldPos) connPos))
                        continue;

                    // Get cells visible from the player's position
                    grid.GetNeighborCells(connPos.cell, neighborCells);

                    // Initialize the observer set for this connection
                    HashSet<NetworkIdentity> observers = new HashSet<NetworkIdentity>();
                    connectionObservers[conn] = observers;

                    // Add all identities in visible cells to the observer set
                    for (int i = 0; i < neighborCells.Length; i++)
                    {
                        int index = GetCellIndex(neighborCells[i]);
                        if (index >= 0 && index < cells.Count && cells[index] != null)
                        {
                            foreach (NetworkIdentity identity in cells[index])
                                observers.Add(identity);
                        }
                    }
                }

                // RebuildAll invokes NetworkServer.RebuildObservers on all spawned objects
                base.RebuildAll();

                // Update the last rebuild time
                lastRebuildTime = NetworkTime.time;
            }
        }

        // Called when a new networked object is spawned on the server
        public override void OnSpawned(NetworkIdentity identity)
        {
            // Register the new object's position in the grid system
            UpdateIdentityPosition(identity);

            // Check if the object is statically batched (indicating it won't move)
            Renderer[] renderers = identity.gameObject.GetComponentsInChildren<Renderer>();
            if (renderers.Any(r => r.isPartOfStaticBatch))
                staticObjects.Add(identity);
        }

        // Updates the grid cell position of a NetworkIdentity when it moves or spawns
        void UpdateIdentityPosition(NetworkIdentity identity)
        {
            // Get the current world position of the object
            Vector3 position = identity.transform.position;

            // Convert player position to grid cell coordinates
            Cell3D newCell = grid.WorldToCell(position);

            // Check if the object is within ±9 km bounds
            if (Mathf.Abs(position.x) > MAX_AREA || Mathf.Abs(position.y) > MAX_AREA || Mathf.Abs(position.z) > MAX_AREA)
                return; // Ignore objects outside bounds

            // Check if the object was previously tracked
            if (lastIdentityPositions.TryGetValue(identity, out (Cell3D cell, Vector3 worldPos) previous))
            {
                // Only update if the position has changed significantly or the cell has changed
                if (Vector3.Distance(position, previous.worldPos) >= minMoveDistance || !newCell.Equals(previous.cell))
                {
                    if (!newCell.Equals(previous.cell))
                    {
                        // Object moved to a new cell
                        // Remove it from the old cell's set and add it to the new cell's set
                        int oldIndex = GetCellIndex(previous.cell);
                        if (oldIndex >= 0 && oldIndex < cells.Count && cells[oldIndex] != null)
                            cells[oldIndex].Remove(identity);
                        AddToCell(newCell, identity);
                    }
                    // Update the stored position and cell
                    lastIdentityPositions[identity] = (newCell, position);
                }
            }
            else
            {
                // New object - add it to the grid and track its position
                AddToCell(newCell, identity);
                lastIdentityPositions[identity] = (newCell, position);
            }
        }

        // Adds a NetworkIdentity to a specific cell's set of objects
        void AddToCell(Cell3D cell, NetworkIdentity identity)
        {
            int index = GetCellIndex(cell);
            if (index < 0 || index >= cells.Count)
                return; // Out of bounds, ignore

            // If the cell doesn't exist in the array yet, fetch or create a new set from the pool
            if (cells[index] == null)
            {
                cells[index] = cellPool.Count > 0 ? cellPool.Pop() : new HashSet<NetworkIdentity>();
            }
            cells[index].Add(identity);
        }

        // Determines if a new observer can see a given NetworkIdentity
        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
        {
            // Check if we have position data for both the object and the observer
            if (!lastIdentityPositions.TryGetValue(identity, out (Cell3D cell, Vector3 worldPos) identityPos) ||
                !lastConnectionPositions.TryGetValue(newObserver, out (Cell3D cell, Vector3 worldPos) observerPos))
                return false; // If not, assume no visibility

            // Populate the pre-allocated array with visible cells from the observer's position
            grid.GetNeighborCells(observerPos.cell, neighborCells);

            // Check if the object's cell is among the visible ones
            for (int i = 0; i < neighborCells.Length; i++)
                if (neighborCells[i].Equals(identityPos.cell))
                    return true;

            return false;
        }

        // Rebuilds the set of observers for a specific NetworkIdentity
        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
        {
            // If the object's position isn't tracked, skip rebuilding
            if (!lastIdentityPositions.TryGetValue(identity, out (Cell3D cell, Vector3 worldPos) identityPos))
                return;

            // Use the precomputed observer sets to determine visibility
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                // Skip if the connection or its identity is null
                if (conn?.identity == null)
                    continue;

                // Check if this connection can observe the identity
                if (connectionObservers.TryGetValue(conn, out HashSet<NetworkIdentity> observers) && observers.Contains(identity))
                    newObservers.Add(conn);
            }
        }

        public override void ResetState()
        {
            lastRebuildTime = 0;
            // Clear and return all cell sets to the pool
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] != null)
                {
                    cells[i].Clear();
                    cellPool.Push(cells[i]);
                    cells[i] = null;
                }
            }
            lastIdentityPositions.Clear();
            lastConnectionPositions.Clear();
            connectionObservers.Clear();
            identityKeys.Clear();
            staticObjects.Clear();
            rebuildCounter = 0;
        }

        public override void OnDestroyed(NetworkIdentity identity)
        {
            // If the object was tracked, remove it from its cell and position records
            if (lastIdentityPositions.TryGetValue(identity, out (Cell3D cell, Vector3 worldPos) pos))
            {
                int index = GetCellIndex(pos.cell);
                if (index >= 0 && index < cells.Count && cells[index] != null)
                {
                    cells[index].Remove(identity);           // Remove from the cell's set
                                                             // If the cell's set is now empty, return it to the pool
                    if (cells[index].Count == 0)
                    {
                        cellPool.Push(cells[index]);
                        cells[index] = null;
                    }
                }
                lastIdentityPositions.Remove(identity);     // Remove from position tracking
                staticObjects.Remove(identity);            // Ensure it's removed from static set if present
            }
        }

        // Computes a unique index for a cell in the sparse array, supporting ±9 km bounds
        int GetCellIndex(Cell3D cell)
        {
            int qOffset = cell.q + MAX_Q / 2; // Shift -9 to 9 -> 0 to 18
            int rOffset = cell.r + MAX_R / 2; // Shift -11 to 11 -> 0 to 22
            int layerOffset = cell.layer + LAYER_OFFSET; // Shift -18 to 17 -> 0 to 35
            return qOffset + rOffset * MAX_Q + layerOffset * MAX_Q * MAX_R;
        }

#if UNITY_EDITOR

        // Draws debug gizmos in the Unity Editor to visualize the grid
        void OnDrawGizmos()
        {
            // Initialize the grid if it hasn't been created yet (e.g., before Awake)
            if (grid == null)
                grid = new HexGrid3D(visRange, cellHeight);

            // Only draw if there's a local player to base the visualization on
            if (NetworkClient.localPlayer != null)
            {
                Vector3 playerPosition = NetworkClient.localPlayer.transform.position;

                // Convert to grid cell
                Cell3D playerCell = grid.WorldToCell(playerPosition);

                // Get all visible cells around the player into the pre-allocated array
                grid.GetNeighborCells(playerCell, neighborCells);

                // Set default gizmo color (though overridden per cell)
                Gizmos.color = Color.cyan;

                // Draw each visible cell as a hexagonal prism
                for (int i = 0; i < neighborCells.Length; i++)
                {
                    // Convert cell to world coordinates
                    Vector3 worldPos = grid.CellToWorld(neighborCells[i]);

                    // Determine the layer relative to the player's cell for color coding
                    int relativeLayer = neighborCells[i].layer - playerCell.layer;

                    // Draw the hexagonal cell with appropriate color based on layer
                    grid.DrawHexGizmo(worldPos, grid.cellRadius, grid.cellHeight, relativeLayer);
                }
            }
        }

#endif
    }
}
