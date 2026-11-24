using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// A regular lattice of chunks over a floor's footprint. The tech plan uses 2 columns
    /// × 4 rows: on a 200 × 160 tower floor every chunk is 100 × 40 m = 4000 m².
    /// Chunk rects tile the bounds exactly, so clipped geometry never leaves a gap.
    /// </summary>
    public sealed class FloorChunkGrid
    {
        public const int DefaultColumns = 2;
        public const int DefaultRows = 4;

        public readonly FloorPlan Plan;
        public readonly int Columns;
        public readonly int Rows;

        readonly float _cellW;
        readonly float _cellH;

        public FloorChunkGrid(FloorPlan plan, int columns = DefaultColumns, int rows = DefaultRows)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            Plan = plan;
            Columns = Mathf.Max(1, columns);
            Rows = Mathf.Max(1, rows);
            RectF b = plan.Bounds;
            _cellW = b.W / Columns;
            _cellH = b.H / Rows;
        }

        public RectF Bounds { get { return Plan.Bounds; } }
        public int Count { get { return Columns * Rows; } }

        public int Index(int column, int row)
        {
            return Mathf.Clamp(row, 0, Rows - 1) * Columns + Mathf.Clamp(column, 0, Columns - 1);
        }

        public int ColumnOf(int index) { return index % Columns; }
        public int RowOf(int index) { return index / Columns; }

        public RectF Rect(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException("index");
            int column = ColumnOf(index), row = RowOf(index);
            RectF b = Bounds;
            return new RectF(b.X + column * _cellW, b.Y + row * _cellH, _cellW, _cellH);
        }

        public Vector2 Center(int index) { return Rect(index).Center; }

        /// <summary>Chunk containing the point; points outside the bounds clamp to the edge chunk.</summary>
        public int IndexAt(Vector2 point)
        {
            RectF b = Bounds;
            int column = Mathf.Clamp(Mathf.FloorToInt((point.x - b.X) / _cellW), 0, Columns - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt((point.y - b.Y) / _cellH), 0, Rows - 1);
            return Index(column, row);
        }

        public int IndexAtWorld(Vector3 point) { return IndexAt(new Vector2(point.x, point.z)); }

        /// <summary>Distance from the point to the chunk rect (0 when inside).</summary>
        public float DistanceTo(int index, Vector2 point)
        {
            RectF r = Rect(index);
            float dx = Mathf.Max(r.X - point.x, 0f, point.x - r.XMax);
            float dz = Mathf.Max(r.Y - point.y, 0f, point.y - r.YMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Chebyshev ring distance between two chunks (1 = directly adjacent).</summary>
        public int RingDistance(int a, int b)
        {
            return Mathf.Max(Mathf.Abs(ColumnOf(a) - ColumnOf(b)), Mathf.Abs(RowOf(a) - RowOf(b)));
        }

        /// <summary>All chunk indices sorted by their distance to the point (stable on ties).</summary>
        public int[] SortedByDistance(Vector2 point)
        {
            int[] order = new int[Count];
            float[] distances = new float[Count];
            for (int i = 0; i < Count; i++)
            {
                order[i] = i;
                distances[i] = DistanceTo(i, point);
            }
            Array.Sort(order, (a, b) =>
            {
                int c = distances[a].CompareTo(distances[b]);
                return c != 0 ? c : a.CompareTo(b);
            });
            return order;
        }

        /// <summary>Chunks whose ring distance to the given chunk is at most the radius.</summary>
        public List<int> Ring(int center, int radius)
        {
            List<int> result = new List<int>();
            if (radius < 0) return result;
            int cc = ColumnOf(center), cr = RowOf(center);
            for (int row = cr - radius; row <= cr + radius; row++)
            {
                if (row < 0 || row >= Rows) continue;
                for (int column = cc - radius; column <= cc + radius; column++)
                {
                    if (column < 0 || column >= Columns) continue;
                    result.Add(Index(column, row));
                }
            }
            return result;
        }

        /// <summary>Every chunk whose rect overlaps the given area (used by coverage checks).</summary>
        public List<int> Overlapping(RectF area)
        {
            List<int> result = new List<int>();
            RectF b = Bounds;
            int c0 = Mathf.Clamp(Mathf.FloorToInt((area.X - b.X) / _cellW), 0, Columns - 1);
            int c1 = Mathf.Clamp(Mathf.FloorToInt((area.XMax - b.X - 0.0001f) / _cellW), 0, Columns - 1);
            int r0 = Mathf.Clamp(Mathf.FloorToInt((area.Y - b.Y) / _cellH), 0, Rows - 1);
            int r1 = Mathf.Clamp(Mathf.FloorToInt((area.YMax - b.Y - 0.0001f) / _cellH), 0, Rows - 1);
            for (int row = r0; row <= r1; row++)
                for (int column = c0; column <= c1; column++)
                    result.Add(Index(column, row));
            return result;
        }
    }
}
