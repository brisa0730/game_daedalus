using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// Box-against-tile collision on the <see cref="TileGrid"/>. A box is given as its feet centre
    /// (x = centre, y = bottom), half width and height. It covers the cells it overlaps, half-open:
    /// a box resting exactly on a face does not overlap the next cell. Solid and Climbable block
    /// both axes; OneWay blocks only a downward move whose start was at or above its top.
    /// Callers sweep X first (at the old height), then Y.
    /// </summary>
    public static class TileCollision
    {
        const float Epsilon = MovementInternals.ContactEpsilon;

        public static bool Blocks(TileKind kind)
        {
            return kind == TileKind.Solid || kind == TileKind.Climbable;
        }

        /// <summary>Moves the box horizontally by <paramref name="dx"/>, stopping with its leading edge on the first blocking face.</summary>
        public static float SweepX(TileGrid grid, float x, float y, float halfWidth, float height, float dx, out int hitSide)
        {
            hitSide = 0;
            if (dx == 0)
            {
                return x;
            }
            int rowFirst = FirstCovered(y);
            int rowLast = LastCovered(y + height);
            if (dx > 0)
            {
                float right = x + halfWidth;
                float end = right + dx;
                for (int c = (int)MathF.Ceiling(right - Epsilon); c < end; c++)
                {
                    if (ColumnBlocks(grid, c, rowFirst, rowLast, true))
                    {
                        hitSide = 1;
                        return c - halfWidth;
                    }
                }
            }
            else
            {
                float left = x - halfWidth;
                float end = left + dx;
                for (int c = (int)MathF.Floor(left + Epsilon) - 1; c + 1 > end; c--)
                {
                    if (ColumnBlocks(grid, c, rowFirst, rowLast, true))
                    {
                        hitSide = -1;
                        return c + 1 + halfWidth;
                    }
                }
            }
            return x + dx;
        }

        /// <summary>
        /// Moves the box vertically by <paramref name="dy"/>. Down: lands on the first Solid/Climbable top, or
        /// OneWay top, and reports what it landed on. Up: stops the head on a blocking cell's bottom and reports
        /// <paramref name="ceiling"/>, unless the ceiling-corner correction slides the box sideways (at most
        /// <see cref="MovementInternals.CeilingCornerCorrection"/>) past it. On a rope (<paramref name="onRope"/>)
        /// one-ways are ignored and the box is never slid off the rope.
        /// </summary>
        public static void SweepY(TileGrid grid, ref float x, ref float y, float halfWidth, float height, float dy,
            bool onRope, out SupportKind landedOn, out bool ceiling)
        {
            landedOn = SupportKind.None;
            ceiling = false;
            if (dy == 0)
            {
                return;
            }
            int colFirst = FirstCovered(x - halfWidth);
            int colLast = LastCovered(x + halfWidth);
            if (dy < 0)
            {
                // Only rows whose top is at or below the start bottom are crossed, which is the one-way rule:
                // a one-way catches the box only if its feet were at or above the top before the move.
                float end = y + dy;
                for (int r = (int)MathF.Floor(y + Epsilon) - 1; r + 1 > end; r--)
                {
                    SupportKind support = RowSupport(grid, r, colFirst, colLast, onRope);
                    if (support != SupportKind.None)
                    {
                        y = r + 1;
                        landedOn = support;
                        return;
                    }
                }
                y = end;
                return;
            }

            float top = y + height;
            float endTop = top + dy;
            for (int r = (int)MathF.Ceiling(top - Epsilon); r < endTop; r++)
            {
                if (!RowBlocks(grid, r, colFirst, colLast))
                {
                    continue;
                }
                if (!onRope && TryCornerCorrection(grid, ref x, y, halfWidth, height, dy, r, colFirst, colLast))
                {
                    y += dy;
                    return;
                }
                y = r - height;
                ceiling = true;
                return;
            }
            y += dy;
        }

        /// <summary>What lies under a box whose bottom is on an integer row (within the contact epsilon). Straddling solid wins.</summary>
        public static SupportKind Support(TileGrid grid, float x, float y, float halfWidth)
        {
            float row = MathF.Round(y);
            if (MathF.Abs(y - row) > Epsilon)
            {
                return SupportKind.None;
            }
            return RowSupport(grid, (int)row - 1, FirstCovered(x - halfWidth), LastCovered(x + halfWidth), false);
        }

        /// <summary>
        /// Distance from the box's side to the nearest wall face on <paramref name="side"/> (−1 left, +1 right)
        /// within <paramref name="reach"/>, else +∞. A wall is Solid (and Climbable when <paramref name="climbableIsWall"/>)
        /// or the map side; OneWay never is.
        /// </summary>
        public static float WallGap(TileGrid grid, float x, float y, float halfWidth, float height, int side, float reach, bool climbableIsWall)
        {
            int rowFirst = FirstCovered(y);
            int rowLast = LastCovered(y + height);
            if (side > 0)
            {
                float right = x + halfWidth;
                for (int c = (int)MathF.Ceiling(right - Epsilon); c - right <= reach + Epsilon; c++)
                {
                    if (ColumnBlocks(grid, c, rowFirst, rowLast, climbableIsWall))
                    {
                        return MathF.Max(0, c - right);
                    }
                }
            }
            else
            {
                float left = x - halfWidth;
                for (int c = (int)MathF.Floor(left + Epsilon) - 1; left - (c + 1) <= reach + Epsilon; c--)
                {
                    if (ColumnBlocks(grid, c, rowFirst, rowLast, climbableIsWall))
                    {
                        return MathF.Max(0, left - (c + 1));
                    }
                }
            }
            return float.PositiveInfinity;
        }

        public static bool IsClimbableAt(TileGrid grid, float x, float y)
        {
            return grid.Get((int)MathF.Floor(x), (int)MathF.Floor(y)) == TileKind.Climbable;
        }

        /// <summary>True when the box overlaps no blocking cell and lies inside the map's sides and top. Below the map is open.</summary>
        public static bool BoxIsFree(TileGrid grid, float x, float y, float halfWidth, float height)
        {
            int rowFirst = FirstCovered(y);
            int rowLast = LastCovered(y + height);
            int colLast = LastCovered(x + halfWidth);
            for (int c = FirstCovered(x - halfWidth); c <= colLast; c++)
            {
                if (ColumnBlocks(grid, c, rowFirst, rowLast, true))
                {
                    return false;
                }
            }
            return true;
        }

        static bool TryCornerCorrection(TileGrid grid, ref float x, float y, float halfWidth, float height, float dy,
            int row, int colFirst, int colLast)
        {
            int minBlocking = int.MaxValue;
            int maxBlocking = int.MinValue;
            for (int c = colFirst; c <= colLast; c++)
            {
                if (Blocks(grid.Get(c, row)))
                {
                    minBlocking = Math.Min(minBlocking, c);
                    maxBlocking = Math.Max(maxBlocking, c);
                }
            }

            // How far the blocking cells reach into the box from each side; at most one can be within the cap.
            float overlapFromLeft = maxBlocking + 1 - (x - halfWidth);
            float overlapFromRight = x + halfWidth - minBlocking;
            float shift;
            if (overlapFromLeft <= MovementInternals.CeilingCornerCorrection)
            {
                shift = overlapFromLeft;
            }
            else if (overlapFromRight <= MovementInternals.CeilingCornerCorrection)
            {
                shift = -overlapFromRight;
            }
            else
            {
                return false;
            }

            float shiftedX = x + shift;
            if (!BoxIsFree(grid, shiftedX, y, halfWidth, height + dy))
            {
                return false;
            }
            x = shiftedX;
            return true;
        }

        static SupportKind RowSupport(TileGrid grid, int row, int colFirst, int colLast, bool ignoreOneWay)
        {
            bool oneWay = false;
            for (int c = colFirst; c <= colLast; c++)
            {
                TileKind kind = grid.Get(c, row);
                if (Blocks(kind))
                {
                    return SupportKind.Solid;
                }
                oneWay |= kind == TileKind.OneWay && !ignoreOneWay;
            }
            return oneWay ? SupportKind.OneWayOnly : SupportKind.None;
        }

        static bool RowBlocks(TileGrid grid, int row, int colFirst, int colLast)
        {
            for (int c = colFirst; c <= colLast; c++)
            {
                if (Blocks(grid.Get(c, row)))
                {
                    return true;
                }
            }
            return false;
        }

        static bool ColumnBlocks(TileGrid grid, int column, int rowFirst, int rowLast, bool climbableIsWall)
        {
            for (int r = rowFirst; r <= rowLast; r++)
            {
                TileKind kind = grid.Get(column, r);
                if (kind == TileKind.Solid || (climbableIsWall && kind == TileKind.Climbable))
                {
                    return true;
                }
            }
            return false;
        }

        static int FirstCovered(float min)
        {
            return (int)MathF.Floor(min + Epsilon);
        }

        static int LastCovered(float max)
        {
            return (int)MathF.Ceiling(max - Epsilon) - 1;
        }
    }
}
