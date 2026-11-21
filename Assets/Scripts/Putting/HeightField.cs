using System;
using UnityEngine;

namespace ARGolf.Putting
{
    /// <summary>
    /// Heights on a regular grid (m), e.g. every 10 cm. Height between grid points is interpolated
    /// (bilinear); slope is the central difference over one cell, so it changes smoothly.
    /// Outside the grid the edge heights continue flat.
    /// Source can be a designed green (GreenDefinition) or measured terrain (e.g. lidar).
    /// </summary>
    public sealed class HeightField
    {
        /// <summary>Position of grid point (0, 0) on the floor plane.</summary>
        public readonly Vector2 Origin;
        public readonly float CellSize;
        public readonly int CountX, CountZ;
        readonly float[] heights;

        public HeightField(Vector2 origin, float cellSize, int countX, int countZ, float[] heights = null)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (countX < 2 || countZ < 2) throw new ArgumentException("Need at least 2 x 2 grid points.");
            Origin = origin;
            CellSize = cellSize;
            CountX = countX;
            CountZ = countZ;
            this.heights = heights ?? new float[countX * countZ];
            if (this.heights.Length != countX * countZ) throw new ArgumentException("heights has the wrong length.");
        }

        /// <summary>Samples a height function on a grid covering min..max.</summary>
        public static HeightField Sample(Vector2 min, Vector2 max, float cellSize, Func<Vector2, float> height)
        {
            int nx = Mathf.Max(2, Mathf.CeilToInt((max.x - min.x) / cellSize) + 1);
            int nz = Mathf.Max(2, Mathf.CeilToInt((max.y - min.y) / cellSize) + 1);
            var field = new HeightField(min, cellSize, nx, nz);
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                    field[i, j] = height(field.PointAt(i, j));
            return field;
        }

        public float this[int i, int j]
        {
            get => heights[i + j * CountX];
            set => heights[i + j * CountX] = value;
        }

        public Vector2 PointAt(int i, int j) => Origin + new Vector2(i * CellSize, j * CellSize);

        public Vector2 Max => PointAt(CountX - 1, CountZ - 1);

        /// <summary>Adds a constant to every height.</summary>
        public void Offset(float dh)
        {
            for (int k = 0; k < heights.Length; k++) heights[k] += dh;
        }

        public float HeightAt(Vector2 p)
        {
            float fx = Mathf.Clamp((p.x - Origin.x) / CellSize, 0f, CountX - 1);
            float fz = Mathf.Clamp((p.y - Origin.y) / CellSize, 0f, CountZ - 1);
            int i = Mathf.Min((int)fx, CountX - 2);
            int j = Mathf.Min((int)fz, CountZ - 2);
            float u = fx - i, w = fz - j;
            float h00 = this[i, j], h10 = this[i + 1, j], h01 = this[i, j + 1], h11 = this[i + 1, j + 1];
            return (h00 * (1f - u) + h10 * u) * (1f - w) + (h01 * (1f - u) + h11 * u) * w;
        }

        public Vector2 SlopeAt(Vector2 p)
        {
            float d = CellSize;
            var dx = new Vector2(d, 0f);
            var dz = new Vector2(0f, d);
            return new Vector2(
                (HeightAt(p + dx) - HeightAt(p - dx)) / (2f * d),
                (HeightAt(p + dz) - HeightAt(p - dz)) / (2f * d));
        }
    }
}
