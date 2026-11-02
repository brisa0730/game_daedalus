using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// Where the player actually starts or respawns: the given feet position if it is free there and inside
    /// the map, else the nearest free cell-centred position within <see cref="MovementInternals.StartSearchRadius"/>
    /// cells that stands on support. Free means the hitbox overlaps no blocking cell and its hazard core touches no
    /// hazard, or a start on spikes would die and come back there forever. A moved position must also stand on
    /// support (hazards never are), or a start on a floor-mounted spike strip would move into the air above it,
    /// fall back in and die forever. An unmoved start may be in mid-air.
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
        /// <summary>False when no free, supported position is in the search radius; X and Y are then the original feet.</summary>
        public bool Found { get; }

        public static StartPlacement Find(TileGrid grid, float feetX, float feetY)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }
            // Feet below row 0 are not free: the player would be in a pit and die there forever.
            if (feetY >= 0 && IsFree(grid, feetX, feetY))
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
                    if (!IsFree(grid, x, y) || TileCollision.Support(grid, x, y, HalfWidth) == SupportKind.None)
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

        static bool IsFree(TileGrid grid, float x, float y)
        {
            return TileCollision.BoxIsFree(grid, x, y, HalfWidth, Height) && !TileCollision.CoreTouchesHazard(grid, x, y);
        }
    }
}
