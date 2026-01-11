using UnityEngine;

namespace Mirror
{
    internal class HexGrid2D
    {
        // Radius of each hexagonal cell (half the width)
        internal float cellRadius;

        // Offset applied to align the grid with the world origin
        Vector2 originOffset;

        // Precomputed constants for hexagon math to improve performance
        readonly float sqrt3Div3; // sqrt(3) / 3, used in coordinate conversions
        readonly float oneDiv3;   // 1 / 3, used in coordinate conversions
        readonly float twoDiv3;   // 2 / 3, used in coordinate conversions
        readonly float sqrt3;     // sqrt(3), used in world coordinate calculations
        readonly float sqrt3Div2; // sqrt(3) / 2, used in world coordinate calculations

        internal HexGrid2D(ushort visRange)
        {
            // Set cell radius as half the visibility range
            cellRadius = visRange / 2f;

            // Offset to center the grid at world origin (2D XZ plane)
            originOffset = Vector2.zero;

            // Precompute mathematical constants for efficiency
            sqrt3Div3 = Mathf.Sqrt(3) / 3f;
            oneDiv3 = 1f / 3f;
            twoDiv3 = 2f / 3f;
            sqrt3 = Mathf.Sqrt(3);
            sqrt3Div2 = Mathf.Sqrt(3) / 2f;
        }

        // Precomputed array of neighbor offsets as Cell2D structs (center + 6 neighbors)
        static readonly Cell2D[] neighborCellsBase = new Cell2D[]
        {
            new Cell2D(0, 0),         // Center
            new Cell2D(1, -1),        // Top-right
            new Cell2D(1, 0),         // Right
            new Cell2D(0, 1),         // Bottom-right
            new Cell2D(-1, 1),        // Bottom-left
            new Cell2D(-1, 0),        // Left
            new Cell2D(0, -1)         // Top-left
        };

        // Converts a grid cell (q, r) to a world position (x, z)
        internal Vector2 CellToWorld(Cell2D cell)
        {
            // Calculate X and Z using hexagonal coordinate formulas
            float x = cellRadius * (sqrt3 * cell.q + sqrt3Div2 * cell.r);
            float z = cellRadius * (1.5f * cell.r);

            // Subtract the origin offset to align with world space and return the position
            return new Vector2(x, z) - originOffset;
        }

        // Converts a world position (x, z) to a grid cell (q, r)
        internal Cell2D WorldToCell(Vector2 position)
        {
            // Apply the origin offset to adjust the position before conversion
            position += originOffset;

            // Convert world X, Z to axial q, r coordinates using inverse hexagonal formulas
            float q = (sqrt3Div3 * position.x - oneDiv3 * position.y) / cellRadius;
            float r = (twoDiv3 * position.y) / cellRadius;

            // Round to the nearest valid cell and return
            return RoundToCell(q, r);
        }

        // Rounds floating-point axial coordinates (q, r) to the nearest integer cell coordinates
        Cell2D RoundToCell(float q, float r)
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

            return new Cell2D(qInt, rInt);
        }

        // Populates the provided array with neighboring cells around a given center cell
        internal void GetNeighborCells(Cell2D center, Cell2D[] neighbors)
        {
            // Ensure the array has the correct size (7: center + 6 neighbors)
            if (neighbors.Length != 7)
                throw new System.ArgumentException("Neighbor array must have exactly 7 elements");

            // Populate the array by adjusting precomputed offsets with the center cell's coordinates
            for (int i = 0; i < neighborCellsBase.Length; i++)
            {
                neighbors[i] = new Cell2D(
                    center.q + neighborCellsBase[i].q,
                    center.r + neighborCellsBase[i].r
                );
            }
        }

#if UNITY_EDITOR
        // Draws a 2D hexagonal gizmo in the Unity Editor for visualization
        internal void DrawHexGizmo(Vector3 center, float radius, HexSpatialHash2DInterestManagement.CheckMethod checkMethod)
        {
            // Hexagon has 6 sides
            const int segments = 6;

            // Array to store the 6 corner points in 3D
            Vector3[] corners = new Vector3[segments];

            // Calculate the corner positions based on the plane (XZ or XY)
            for (int i = 0; i < segments; i++)
            {
                // Angle for each corner, offset by 90 degrees
                float angle = 2 * Mathf.PI / segments * i + Mathf.PI / 2;

                if (checkMethod == HexSpatialHash2DInterestManagement.CheckMethod.XZ_FOR_3D)
                {
                    // XZ plane: flat hexagon, Y=0
                    corners[i] = center + new Vector3(radius * Mathf.Cos(angle), 0, radius * Mathf.Sin(angle));
                }
                else // XY_FOR_2D
                {
                    // XY plane: vertical hexagon, Z=0
                    corners[i] = center + new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0);
                }
            }

            // Draw each side of the hexagon
            for (int i = 0; i < segments; i++)
            {
                Vector3 cornerA = corners[i];
                Vector3 cornerB = corners[(i + 1) % segments];
                Gizmos.DrawLine(cornerA, cornerB);
            }
        }
#endif
    }

    // Struct representing a single cell in the 2D hexagonal grid
    internal struct Cell2D
    {
        internal readonly int q; // Axial q coordinate (horizontal axis)
        internal readonly int r; // Axial r coordinate (diagonal axis)

        internal Cell2D(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public override bool Equals(object obj) =>
            obj is Cell2D other && q == other.q && r == other.r;

        // Generate a unique hash code for the cell
        public override int GetHashCode() => (q << 16) ^ r;
    }
}