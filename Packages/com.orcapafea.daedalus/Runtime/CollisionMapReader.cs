using System.Collections.Generic;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Reads the Collision Tilemap into a <see cref="CollisionMap"/> at play start. Writes nothing:
    /// never <c>CompressBounds</c>, never a tile. Only the given Tilemap is read.
    /// </summary>
    internal static class CollisionMapReader
    {
        // Keeps an include edge that lies exactly on a cell boundary out of the next cell.
        const float EdgeInset = 1e-4f;

        /// <summary>
        /// The map covers the Tilemap's painted cells grown to contain <paramref name="includeWorld"/> (the start
        /// hitbox and the ropes; a zero-size rect includes nothing), plus <see cref="MovementInternals.MapHeadroomRows"/>
        /// rows on top. Plain tiles are Solid; a <see cref="DaedalusTile"/> has its own kind. Grid problems are added
        /// to <paramref name="problems"/> as warning texts.
        /// </summary>
        public static CollisionMap Read(Tilemap tilemap, Rect includeWorld, List<string> problems)
        {
            CheckGrid(tilemap, problems);

            BoundsInt authored = tilemap.cellBounds;
            bool hasCells = authored.size.x > 0 && authored.size.y > 0 && authored.size.z > 0;
            TileBase[] tiles = hasCells ? tilemap.GetTilesBlock(authored) : new TileBase[0];
            int sizeX = authored.size.x;
            int sizeY = authored.size.y;
            // The painted cells, not cellBounds itself: a Tilemap's bounds grow from cell (0, 0) and never
            // shrink when tiles are erased (CompressBounds would fix that by writing authored data).
            var cells = new CellRange();
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null)
                {
                    cells.Add(authored.xMin + i % sizeX, authored.yMin + i / sizeX % sizeY);
                }
            }
            if (!cells.Any)
            {
                problems.Add(PlayerWarnings.EmptyTilemap);
            }
            AddWorldRect(cells, includeWorld, world => tilemap.WorldToCell(world));
            if (!cells.Any)
            {
                var single = new Vector2Int(authored.xMin, authored.yMin);
                return new CollisionMap(new TileGrid(1, 1), tilemap.CellToWorld(new Vector3Int(single.x, single.y, 0)), single);
            }

            var cellMin = new Vector2Int(cells.MinX, cells.MinY);
            var grid = new TileGrid(cells.MaxX - cells.MinX + 1, cells.MaxY - cells.MinY + 1 + MovementInternals.MapHeadroomRows);
            var kinds = new Dictionary<TileBase, TileKind>();
            for (int i = 0; i < tiles.Length; i++)
            {
                TileBase tile = tiles[i];
                if (tile == null)
                {
                    continue;
                }
                int x = authored.xMin + i % sizeX;
                int y = authored.yMin + i / sizeX % sizeY;
                grid.Set(x - cellMin.x, y - cellMin.y, KindOf(tile, kinds));
            }

            Vector3 origin = tilemap.CellToWorld(new Vector3Int(cellMin.x, cellMin.y, 0));
            return new CollisionMap(grid, origin, cellMin);
        }

        /// <summary>
        /// The map when there is no Collision Tilemap at all: empty cells around <paramref name="includeWorld"/>
        /// (on a 1 × 1 grid at the world origin) plus headroom, so the player falls into the pit below and dies.
        /// </summary>
        public static CollisionMap Empty(Rect includeWorld)
        {
            var cells = new CellRange();
            AddWorldRect(cells, includeWorld, world => Vector3Int.FloorToInt(world));
            if (!cells.Any)
            {
                return new CollisionMap(new TileGrid(1, 1), Vector2.zero, Vector2Int.zero);
            }
            var cellMin = new Vector2Int(cells.MinX, cells.MinY);
            var grid = new TileGrid(cells.MaxX - cells.MinX + 1, cells.MaxY - cells.MinY + 1 + MovementInternals.MapHeadroomRows);
            return new CollisionMap(grid, cellMin, cellMin);
        }

        static TileKind KindOf(TileBase tile, Dictionary<TileBase, TileKind> kinds)
        {
            if (!kinds.TryGetValue(tile, out TileKind kind))
            {
                kind = tile is DaedalusTile daedalusTile ? DaedalusTile.ToSimKind(daedalusTile.Kind) : TileKind.Solid;
                kinds.Add(tile, kind);
            }
            return kind;
        }

        static void AddWorldRect(CellRange cells, Rect world, System.Func<Vector3, Vector3Int> toCell)
        {
            if (world.width <= 0 && world.height <= 0)
            {
                return;
            }
            Vector3Int min = toCell(new Vector3(world.xMin, world.yMin, 0));
            Vector3Int max = toCell(new Vector3(world.xMax - EdgeInset, world.yMax - EdgeInset, 0));
            cells.Add(min.x, min.y);
            cells.Add(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        static void CheckGrid(Tilemap tilemap, List<string> problems)
        {
            GridLayout layout = tilemap.layoutGrid;
            if (layout != null)
            {
                Vector3 cellSize = layout.cellSize;
                if (!Mathf.Approximately(cellSize.x, 1f) || !Mathf.Approximately(cellSize.y, 1f))
                {
                    problems.Add(PlayerWarnings.CellSize(cellSize));
                }
                if (layout.cellLayout != GridLayout.CellLayout.Rectangle)
                {
                    problems.Add(PlayerWarnings.CellLayout);
                }
            }

            Transform transform = tilemap.transform;
            Vector3 scale = transform.lossyScale;
            bool rotated = transform.rotation != Quaternion.identity;
            bool scaled = !Mathf.Approximately(scale.x, 1f) || !Mathf.Approximately(scale.y, 1f);
            if (rotated || scaled)
            {
                problems.Add(PlayerWarnings.RotatedOrScaled);
            }
        }

        /// <summary>A growing inclusive range of cells.</summary>
        sealed class CellRange
        {
            public bool Any;
            public int MinX, MinY, MaxX, MaxY;

            public void Add(int x, int y)
            {
                if (!Any)
                {
                    Any = true;
                    MinX = MaxX = x;
                    MinY = MaxY = y;
                    return;
                }
                MinX = Mathf.Min(MinX, x);
                MinY = Mathf.Min(MinY, y);
                MaxX = Mathf.Max(MaxX, x);
                MaxY = Mathf.Max(MaxY, y);
            }
        }
    }
}
