using System;
using System.Collections.Generic;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Builds a test map from rows of characters, top row first:
    /// <c>.</c> none, <c>#</c> solid, <c>-</c> one-way, <c>C</c> climbable, <c>R</c> empty + rope,
    /// <c>+</c> one-way + rope, <c>P</c> empty + the player start (feet at the cell's bottom centre).
    /// Each vertical run of <c>R</c>/<c>+</c> is one rope; ropes are ordered by (column, bottom).
    /// </summary>
    sealed class AsciiLevel
    {
        AsciiLevel(TileGrid grid, RopeSegment[] ropes, float startX, float startY)
        {
            Grid = grid;
            Ropes = ropes;
            StartX = startX;
            StartY = startY;
        }

        public TileGrid Grid { get; }
        public RopeSegment[] Ropes { get; }
        public float StartX { get; }
        public float StartY { get; }

        public static AsciiLevel Parse(params string[] rows)
        {
            return Build(rows, true);
        }

        /// <summary>The map alone, for collision tests; a 'P' is allowed but not required.</summary>
        public static TileGrid ParseGrid(params string[] rows)
        {
            return Build(rows, false).Grid;
        }

        /// <summary>Map rows (top first) from a cell function of (column, row from the bottom).</summary>
        public static string[] Rows(int width, int height, Func<int, int, char> cell)
        {
            var rows = new string[height];
            for (int i = 0; i < height; i++)
            {
                var chars = new char[width];
                for (int x = 0; x < width; x++)
                {
                    chars[x] = cell(x, height - 1 - i);
                }
                rows[i] = new string(chars);
            }
            return rows;
        }

        static AsciiLevel Build(string[] rows, bool requireStart)
        {
            if (rows == null || rows.Length == 0)
            {
                throw new ArgumentException("A level needs at least one row.");
            }
            int width = rows[0].Length;
            int height = rows.Length;
            for (int i = 0; i < height; i++)
            {
                if (rows[i].Length != width)
                {
                    throw new ArgumentException($"Row {i} is {rows[i].Length} wide; expected {width}.");
                }
            }

            var grid = new TileGrid(width, height);
            var ropes = new List<RopeSegment>();
            int starts = 0;
            float startX = 0, startY = 0;

            for (int x = 0; x < width; x++)
            {
                int runBottom = -1;
                for (int y = 0; y < height; y++)
                {
                    char ch = rows[height - 1 - y][x];
                    bool rope = ch == 'R' || ch == '+';
                    if (rope && runBottom < 0)
                    {
                        runBottom = y;
                    }
                    else if (!rope && runBottom >= 0)
                    {
                        ropes.Add(new RopeSegment(x + 0.5f, runBottom, y));
                        runBottom = -1;
                    }

                    switch (ch)
                    {
                        case '.':
                        case 'R':
                            break;
                        case '#':
                            grid.Set(x, y, TileKind.Solid);
                            break;
                        case '-':
                        case '+':
                            grid.Set(x, y, TileKind.OneWay);
                            break;
                        case 'C':
                            grid.Set(x, y, TileKind.Climbable);
                            break;
                        case 'P':
                            starts++;
                            startX = x + 0.5f;
                            startY = y;
                            break;
                        default:
                            throw new ArgumentException($"Unknown map character '{ch}' at column {x}, row {y}.");
                    }
                }
                if (runBottom >= 0)
                {
                    ropes.Add(new RopeSegment(x + 0.5f, runBottom, height));
                }
            }

            if (starts > 1 || (requireStart && starts != 1))
            {
                throw new ArgumentException($"A level needs exactly one 'P'; found {starts}.");
            }
            return new AsciiLevel(grid, ropes.ToArray(), startX, startY);
        }
    }
}
