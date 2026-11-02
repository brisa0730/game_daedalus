using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// The map's cells, 0-based, y up. The runtime fills it at play start and keeps the
    /// Tilemap's origin as a render offset. Outside the grid, the sides and the top read
    /// Solid and the bottom reads None (falling out is open).
    /// Three layers, in order: painted cells; the cells of the traps that are present and at rest
    /// (<see cref="LevelStepper"/> stamps them), which <see cref="Get"/> reads only where nothing is painted, so painted
    /// tiles stay in charge of cells; and the moving boxes (<see cref="Boxes"/>), traps in motion between cells,
    /// which block wherever they are (<see cref="TileCollision"/> tests them).
    /// </summary>
    public sealed class TileGrid
    {
        readonly TileKind[] cells;
        readonly TileKind[] trapCells;

        public TileGrid(int width, int height)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(width < 1 ? nameof(width) : nameof(height), $"A tile grid needs at least one cell; got {width} x {height}.");
            }
            Width = width;
            Height = height;
            cells = new TileKind[width * height];
            trapCells = new TileKind[width * height];
            Boxes = new MovingBoxes(0);
        }

        public int Width { get; }
        public int Height { get; }
        /// <summary>The moving-box layer; empty until a <see cref="LevelStepper"/> attaches its own.</summary>
        public MovingBoxes Boxes { get; private set; }

        public void AttachBoxes(MovingBoxes boxes)
        {
            Boxes = boxes ?? throw new ArgumentNullException(nameof(boxes));
        }

        public void Set(int x, int y, TileKind kind)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Cell ({x}, {y}) is outside the {Width} x {Height} grid.");
            }
            cells[y * Width + x] = kind;
        }

        /// <summary>The painted kind, else the trap layer's.</summary>
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
            TileKind painted = cells[y * Width + x];
            return painted != TileKind.None ? painted : trapCells[y * Width + x];
        }

        /// <summary>The painted kind only (no traps); outside the grid as <see cref="Get"/>.</summary>
        public TileKind GetPainted(int x, int y)
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

        /// <summary>Sets a trap-layer cell; cells outside the grid are ignored (traps are clipped to the map).</summary>
        public void SetTrapCell(int x, int y, TileKind kind)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return;
            }
            trapCells[y * Width + x] = kind;
        }

        public void ClearTrapCells()
        {
            Array.Clear(trapCells, 0, trapCells.Length);
        }
    }
}
