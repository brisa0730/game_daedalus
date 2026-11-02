using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// How Move and Fall traps travel, one fixed tick at a time (systems.md § Traps, Move and Fall). Pure and
    /// allocation-free; the level step and the editor's previews and help texts share it, so a preview matches play.
    /// </summary>
    public static class TrapMotion
    {
        const float Dt = MovementInternals.TickSeconds;
        const float Epsilon = MovementInternals.ContactEpsilon;

        /// <summary>Tiles a Move trap travels per tick.</summary>
        public static float MoveStep(float speed)
        {
            return speed * Dt;
        }

        /// <summary>Ticks a Move takes to arrive; 0 for a zero offset.</summary>
        public static int MoveTicks(int offsetX, int offsetY, float speed)
        {
            if (offsetX == 0 && offsetY == 0)
            {
                return 0;
            }
            return (int)MathF.Ceiling(Length(offsetX, offsetY) / MoveStep(speed));
        }

        /// <summary>
        /// A Move's box min after <paramref name="tick"/> ticks, from the tick count (never accumulated). At arrival it is
        /// exactly <paramref name="start"/> + the offset.
        /// </summary>
        public static void MovePosition(Box start, int offsetX, int offsetY, float speed, int tick,
            out float minX, out float minY, out bool arrived)
        {
            arrived = tick >= MoveTicks(offsetX, offsetY, speed);
            if (arrived)
            {
                minX = start.MinX + offsetX;
                minY = start.MinY + offsetY;
                return;
            }
            float length = Length(offsetX, offsetY);
            float f = MathF.Min(tick * MoveStep(speed), length) / length;
            minX = start.MinX + offsetX * f;
            minY = start.MinY + offsetY * f;
        }

        /// <summary>
        /// One tick of a Fall: <paramref name="vy"/> (downward, tiles/s) grows by <paramref name="riseGravity"/> up to
        /// <paramref name="maxFall"/>, then the box drops. It lands on the first painted Solid or Climbable row in its
        /// columns (one-way, trap cells and moving boxes are fallen through): <paramref name="newMinY"/> is then exactly
        /// that row's top and <paramref name="vy"/> 0. A bottom already on or inside such a row lands on its top at once.
        /// Returns true when it landed.
        /// </summary>
        public static bool FallStep(TileGrid grid, Box box, float riseGravity, float maxFall, ref float vy, out float newMinY)
        {
            vy = MathF.Min(vy + riseGravity * Dt, maxFall);
            float bottom = box.MinY;
            float end = bottom - vy * Dt;
            int colFirst = TileCollision.FirstCovered(box.MinX);
            int colLast = TileCollision.LastCovered(box.MaxX);
            // The row holding the bottom first (on its top or inside it), then each row whose top the move crosses.
            for (int r = (int)MathF.Floor(bottom - Epsilon); r + 1 > end && r >= 0; r--)
            {
                if (RowLands(grid, r, colFirst, colLast))
                {
                    newMinY = r + 1;
                    vy = 0;
                    return true;
                }
            }
            newMinY = end;
            return false;
        }

        /// <summary>The first painted Solid/Climbable row at or below the box's bottom (a row the bottom is inside included); false = it falls out of the map.</summary>
        public static bool TryLandingRow(TileGrid grid, Box box, out int row)
        {
            int colFirst = TileCollision.FirstCovered(box.MinX);
            int colLast = TileCollision.LastCovered(box.MaxX);
            for (int r = (int)MathF.Floor(box.MinY - Epsilon); r >= 0; r--)
            {
                if (RowLands(grid, r, colFirst, colLast))
                {
                    row = r;
                    return true;
                }
            }
            row = -1;
            return false;
        }

        /// <summary>Ticks a Fall from rest takes to land, by the same arithmetic as the level step; 0 = already resting, −1 = it falls out of the map.</summary>
        public static int FallTicks(TileGrid grid, Box box, float riseGravity, float maxFall)
        {
            if (!TryLandingRow(grid, box, out int row))
            {
                return -1;
            }
            if (row + 1 >= box.MinY - Epsilon)
            {
                return 0;
            }
            if (!(riseGravity > 0))
            {
                throw new ArgumentOutOfRangeException(nameof(riseGravity), riseGravity, "A fall needs gravity.");
            }
            float height = box.MaxY - box.MinY;
            float vy = 0;
            for (int ticks = 1; ; ticks++)
            {
                if (FallStep(grid, box, riseGravity, maxFall, ref vy, out float minY))
                {
                    return ticks;
                }
                box = new Box(box.MinX, minY, box.MaxX, minY + height);
            }
        }

        static float Length(int offsetX, int offsetY)
        {
            return MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);
        }

        static bool RowLands(TileGrid grid, int row, int colFirst, int colLast)
        {
            for (int c = colFirst; c <= colLast; c++)
            {
                if (TileCollision.Blocks(grid.GetPainted(c, row)))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
