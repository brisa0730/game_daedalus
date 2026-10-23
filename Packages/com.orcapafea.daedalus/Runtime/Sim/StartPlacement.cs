using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// Where the player actually starts: the given feet position if the hitbox is free there and inside
    /// the map, else the nearest free cell-centred position within <see cref="MovementInternals.StartSearchRadius"/> cells.
    /// Play-start only.
    /// </summary>
    public readonly struct StartPlacement
    {
        const float HalfWidth = MovementInternals.HitboxHalfWidth;
        const float Height = MovementInternals.HitboxHeight;

        StartPlacement(float x, float y, bool moved, bool found)
        {
            X = x;
            Y = y;
            Moved = moved;
            Found = found;
        }

        public float X { get; }
        public float Y { get; }
        /// <summary>The start was blocked (whether or not a free position was found).</summary>
        public bool Moved { get; }
        /// <summary>False when every position in the search radius is blocked; X and Y are then the original feet.</summary>
        public bool Found { get; }

        public static StartPlacement Find(TileGrid grid, float feetX, float feetY)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }
            // Feet below row 0 are not free: the player would fall out and reset there forever.
            if (feetY >= 0 && TileCollision.BoxIsFree(grid, feetX, feetY, HalfWidth, Height))
            {
                return new StartPlacement(feetX, feetY, false, true);
            }

            int radius = MovementInternals.StartSearchRadius;
            int cx = (int)MathF.Floor(feetX);
            int cy = (int)MathF.Floor(feetY);
            bool found = false;
            float bestX = feetX, bestY = feetY, bestDistance = float.MaxValue;
            // Higher rows first, then lower columns, and only a strictly nearer cell replaces the best:
            // ties go up, then left.
            for (int r = cy + radius; r >= cy - radius; r--)
            {
                for (int c = cx - radius; c <= cx + radius; c++)
                {
                    if (c < 0 || c >= grid.Width || r < 0 || r >= grid.Height)
                    {
                        continue;
                    }
                    float x = c + 0.5f;
                    float y = r;
                    if (!TileCollision.BoxIsFree(grid, x, y, HalfWidth, Height))
                    {
                        continue;
                    }
                    float distance = (x - feetX) * (x - feetX) + (y - feetY) * (y - feetY);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestX = x;
                        bestY = y;
                        found = true;
                    }
                }
            }
            return new StartPlacement(bestX, bestY, true, found);
        }
    }
}
