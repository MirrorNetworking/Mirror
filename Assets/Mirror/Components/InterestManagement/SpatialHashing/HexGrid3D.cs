using UnityEngine;

namespace Mirror
{
    internal class HexGrid3D
    {
        // Radius of each hexagonal cell (half the width)
        internal float cellRadius;

        // Height of each cell along the Y-axis
        internal float cellHeight;

        // Offset applied to align the grid with the world origin
        Vector3 originOffset;

        // Precomputed constants for hexagon math to improve performance
        readonly float sqrt3Div3; // sqrt(3) / 3, used in coordinate conversions
        readonly float oneDiv3;   // 1 / 3, used in coordinate conversions
        readonly float twoDiv3;   // 2 / 3, used in coordinate conversions
        readonly float sqrt3;     // sqrt(3), used in world coordinate calculations
        readonly float sqrt3Div2; // sqrt(3) / 2, used in world coordinate calculations

        internal HexGrid3D(ushort visRange, ushort height)
        {
            // Set cell radius as half the visibility range
            cellRadius = visRange / 2f;

            // Cell3D height is absolute...don't double it
            cellHeight = height;

            // Offset to center the grid at world origin
            // Cell3D height must be divided by 2 for vertical centering
            originOffset = new Vector3(0, -cellHeight / 2, 0);

            // Precompute mathematical constants for efficiency
            sqrt3Div3 = Mathf.Sqrt(3) / 3f;
            oneDiv3 = 1f / 3f;
            twoDiv3 = 2f / 3f;
            sqrt3 = Mathf.Sqrt(3);
            sqrt3Div2 = Mathf.Sqrt(3) / 2f;
        }

        // Precomputed array of neighbor offsets as Cell3D structs (center + 6 per layer x 3 layers)
        static readonly Cell3D[] neighborCellsBase = new Cell3D[]
        {
        // Center
        new Cell3D(0, 0, 0),
        // Upper layer (1) and its 6 neighbors
        new Cell3D(0, 0, 1),
        new Cell3D(1, -1, 1), new Cell3D(1, 0, 1), new Cell3D(0, 1, 1),
        new Cell3D(-1, 1, 1), new Cell3D(-1, 0, 1), new Cell3D(0, -1, 1),
        // Same layer (0) - 6 neighbors
        new Cell3D(1, -1, 0), new Cell3D(1, 0, 0), new Cell3D(0, 1, 0),
        new Cell3D(-1, 1, 0), new Cell3D(-1, 0, 0), new Cell3D(0, -1, 0),
        // Lower layer (-1) and its 6 neighbors
        new Cell3D(0, 0, -1),
        new Cell3D(1, -1, -1), new Cell3D(1, 0, -1), new Cell3D(0, 1, -1),
        new Cell3D(-1, 1, -1), new Cell3D(-1, 0, -1), new Cell3D(0, -1, -1)
        };

        // Converts a grid cell (q, r, layer) to a world position (x, y, z)
        internal Vector3 CellToWorld(Cell3D cell)
        {
            // Calculate X and Z using hexagonal coordinate formulas
            float x = cellRadius * (sqrt3 * cell.q + sqrt3Div2 * cell.r);
            float z = cellRadius * (1.5f * cell.r);

            // Calculate Y based on layer and cell height
            float y = cell.layer * cellHeight + cellHeight / 2;

            // Subtract the origin offset to align with world space and return the position
            return new Vector3(x, y, z) - originOffset;
        }

        // Converts a world position (x, y, z) to a grid cell (q, r, layer)
        internal Cell3D WorldToCell(Vector3 position)
        {
            // Apply the origin offset to adjust the position before conversion
            position += originOffset;

            // Calculate the vertical layer based on Y position
            int layer = Mathf.FloorToInt(position.y / cellHeight);

            // Convert world X, Z to axial q, r coordinates using inverse hexagonal formulas
            float q = (sqrt3Div3 * position.x - oneDiv3 * position.z) / cellRadius;
            float r = (twoDiv3 * position.z) / cellRadius;

            // Round to the nearest valid cell and return
            return RoundToCell(q, r, layer);
        }

        // Rounds floating-point axial coordinates (q, r) to the nearest integer cell coordinates
        Cell3D RoundToCell(float q, float r, int layer)
        {
            // Calculate the third hexagonal coordinate (s) for consistency
            float s = -q - r;
            int qInt = Mathf.RoundToInt(q); // Round q to nearest integer
            int rInt = Mathf.RoundToInt(r); // Round r to nearest integer
            int sInt = Mathf.RoundToInt(s); // Round s to nearest integer

            // Calculate differences to determine which coordinate needs adjustment
            float qDiff = Mathf.Abs(q - qInt);
            float rDiff = Mathf.Abs(r - rInt);
            float sDiff = Mathf.Abs(s - sInt);

            // Adjust q or r based on which has the largest rounding error (ensures q + r + s = 0)
            if (qDiff > rDiff && qDiff > sDiff)
                qInt = -rInt - sInt; // Adjust q if it has the largest error
            else if (rDiff > sDiff)
                rInt = -qInt - sInt; // Adjust r if it has the largest error

            return new Cell3D(qInt, rInt, layer);
        }

        // Populates the provided array with neighboring cells around a given center cell
        internal void GetNeighborCells(Cell3D center, Cell3D[] neighbors)
        {
            // Ensure the array has the correct size
            if (neighbors.Length != 21)
                throw new System.ArgumentException("Neighbor array must have exactly 21 elements");

            // Populate the array by adjusting precomputed offsets with the center cell's coordinates
            for (int i = 0; i < neighborCellsBase.Length; i++)
            {
                neighbors[i] = new Cell3D(
                    center.q + neighborCellsBase[i].q,
                    center.r + neighborCellsBase[i].r,
                    center.layer + neighborCellsBase[i].layer
                );
            }
        }

#if UNITY_EDITOR

        // Draws a hexagonal gizmo in the Unity Editor for visualization
        internal void DrawHexGizmo(Vector3 center, float radius, float height, int relativeLayer)
        {
            // Hexagon has 6 sides
            const int segments = 6;

            // Array to store the 6 corner points
            Vector3[] corners = new Vector3[segments];

            // Calculate the corner positions of the hexagon in the XZ plane
            for (int i = 0; i < segments; i++)
            {
                // Angle for each corner, offset by 90 degrees
                float angle = 2 * Mathf.PI / segments * i + Mathf.PI / 2;

                // Calculate the corner position based on the angle and radius
                corners[i] = center + new Vector3(radius * Mathf.Cos(angle), 0, radius * Mathf.Sin(angle));
            }

            // Set gizmo color based on the relative layer for easy identification
            Color gizmoColor;
            switch (relativeLayer)
            {
                case 1:
                    gizmoColor = Color.green;   // Upper layer (positive Y)
                    break;
                case 0:
                    gizmoColor = Color.cyan;    // Same layer as the reference point
                    break;
                case -1:
                    gizmoColor = Color.yellow;  // Lower layer (negative Y)
                    break;
                default:
                    gizmoColor = Color.red;     // Fallback for unexpected layers
                    break;
            }

            // Store the current Gizmos color to restore later
            Color previousColor = Gizmos.color;

            // Apply the chosen color
            Gizmos.color = gizmoColor;

            // Draw each side of the hexagon as a 3D quad (wall)
            for (int i = 0; i < segments; i++)
            {
                // Current corner
                Vector3 cornerA = corners[i];

                // Next corner (wraps around at 6)
                Vector3 cornerB = corners[(i + 1) % segments];

                // Calculate top and bottom corners to form a vertical quad
                Vector3 cornerATop = cornerA + Vector3.up * (height / 2);
                Vector3 cornerBTop = cornerB + Vector3.up * (height / 2);
                Vector3 cornerABottom = cornerA - Vector3.up * (height / 2);
                Vector3 cornerBBottom = cornerB - Vector3.up * (height / 2);

                // Draw the four lines of the quad to visualize the wall
                Gizmos.DrawLine(cornerATop, cornerBTop);
                Gizmos.DrawLine(cornerBTop, cornerBBottom);
                Gizmos.DrawLine(cornerBBottom, cornerABottom);
                Gizmos.DrawLine(cornerABottom, cornerATop);
            }

            // Restore the original Gizmos color
            Gizmos.color = previousColor;
        }

#endif
    }

    // Custom struct for neighbor offsets (reduced memory usage)
    internal struct HexOffset
    {
        internal int qOffset;   // Offset in the q (axial) coordinate
        internal int rOffset;   // Offset in the r (axial) coordinate

        internal HexOffset(int q, int r)
        {
            qOffset = q;
            rOffset = r;
        }
    }

    // Struct representing a single cell in the 3D hexagonal grid
    internal struct Cell3D
    {
        internal readonly int q;        // Axial q coordinate (horizontal axis)
        internal readonly int r;        // Axial r coordinate (diagonal axis)
        internal readonly int layer;    // Vertical layer index (Y-axis stacking)

        internal Cell3D(int q, int r, int layer)
        {
            this.q = q;
            this.r = r;
            this.layer = layer;
        }

        public override bool Equals(object obj) =>
            obj is Cell3D other
            && q == other.q
            && r == other.r
            && layer == other.layer;

        // Generate a unique hash code for the cell
        public override int GetHashCode() => (q << 16) ^ (r << 8) ^ layer;
    }
}
