using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// The map's cells, 0-based, y up. The runtime fills it at play start and keeps the
    /// Tilemap's origin as a render offset. Outside the grid, the sides and the top read
    /// Solid and the bottom reads None (falling out is open).
    /// </summary>
    public sealed class TileGrid
    {
        readonly TileKind[] cells;

        public TileGrid(int width, int height)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(width < 1 ? nameof(width) : nameof(height), $"A tile grid needs at least one cell; got {width} x {height}.");
            }
            Width = width;
            Height = height;
            cells = new TileKind[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        public void Set(int x, int y, TileKind kind)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Cell ({x}, {y}) is outside the {Width} x {Height} grid.");
            }
            cells[y * Width + x] = kind;
        }

        public TileKind Get(int x, int y)
        {
            if (x < 0 || x >= Width || y >= Height)
            {
                return TileKind.Solid;
            }
            if (y < 0)
            {
                return TileKind.None;
            }
            return cells[y * Width + x];
        }
    }
}
